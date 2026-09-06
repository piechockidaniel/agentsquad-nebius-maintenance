using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSquad.Agents.Maintenance;

/// <summary>
/// Coordinates the two v1 specialists: Dependency Sentinel establishes evidence; Patch & Verify
/// performs the only allowed mutation in an isolated Nebius Sandbox. The host workspace is never
/// modified by this flow.
/// </summary>
public sealed class MaintenanceService(
    MaintenanceRunStore store,
    MaintenancePolicy policy,
    IPackageSecurityLookup securityLookup,
    IMaintenanceSandbox sandbox,
    IMaintenanceModelProbe modelProbe,
    IMaintenanceNarrator narrator,
    IOptions<AgentSquadOptions> options,
    ILogger<MaintenanceService> logger)
{
    private readonly MaintenanceOptions _options = options.Value.Maintenance;

    public MaintenanceStartResult Start(string trigger, string? scenarioId = null)
    {
        var requestedScenarioId = scenarioId ?? _options.DefaultScenarioId;
        if (!MaintenanceScenarioCatalog.TryGet(requestedScenarioId, out var scenario))
        {
            return new MaintenanceStartResult(false, _options.Enabled, null,
                $"Unknown maintenance scenario '{requestedScenarioId}'.", 400);
        }

        if (!_options.Enabled)
        {
            return new MaintenanceStartResult(false, false, null,
                "Maintenance v1 is disabled. Set AgentSquad:Maintenance:Enabled=true after configuring Nebius credentials.");
        }

        var run = store.TryStart(trigger, scenario.Id);
        if (run is null)
        {
            return new MaintenanceStartResult(false, true, null, "A maintenance run is already active.");
        }

        _ = Task.Run(() => ExecuteAsync(run.Id));
        return new MaintenanceStartResult(true, true, run);
    }

    public MaintenanceRunSnapshot? Get(string id) => store.Get(id);
    public IReadOnlyList<MaintenanceRunSnapshot> Recent(int? limit) => store.Recent(limit);

    public async Task<MaintenancePreflight> GetPreflightAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new MaintenancePreflight(false, _options.ModelId,
                "Maintenance v1 is disabled.", "Nebius Sandbox was not checked.", []);
        }

        var model = await modelProbe.ProbeAsync(cancellationToken);
        var sandboxState = await sandbox.PreflightAsync(cancellationToken);
        return new MaintenancePreflight(
            model.Available && sandboxState.Available,
            _options.ModelId,
            model.Detail,
            sandboxState.Detail,
            sandboxState.AvailableTools);
    }

    private async Task ExecuteAsync(string runId)
    {
        try
        {
            store.Update(runId, run => run.Begin(MaintenanceRunStatuses.Scanning, "Dependency Sentinel: reading fixture"));
            if (!MaintenanceScenarioCatalog.TryGet(store.Get(runId)?.ScenarioId, out var scenario))
            {
                Block(runId, "The requested maintenance scenario is not registered.");
                return;
            }

            var fixtureRoot = ResolveFixtureRoot(scenario);
            var manifestPath = Path.Combine(fixtureRoot, MaintenancePolicy.FixtureProjectFileName);
            var testProjectPath = Path.Combine(fixtureRoot, MaintenancePolicy.FixtureTestProjectFileName);
            if (!File.Exists(manifestPath) || !File.Exists(testProjectPath))
            {
                Block(runId, "The controlled fixture is incomplete in this deployment.");
                return;
            }

            var manifest = await File.ReadAllTextAsync(manifestPath);
            store.Update(runId, run => run.AddStep(
                "read_manifest", "ok", "Dependency Sentinel read the configured fixture manifest."));

            var manifestDecision = policy.EvaluateManifest(manifest);
            store.Update(runId, run => run.AddStep(
                "enforce_patch_policy",
                manifestDecision.Allowed ? "ok" : "blocked",
                manifestDecision.Detail));
            if (!manifestDecision.Allowed)
            {
                Block(runId, manifestDecision.Detail);
                return;
            }

            var assessment = await securityLookup.InspectAsync(
                "NUGET", MaintenancePolicy.PackageId, MaintenancePolicy.VulnerableVersion);
            store.Update(runId, run => run.AddStep(
                "query_nuget_and_ghsa",
                assessment.Available ? "ok" : "blocked",
                assessment.Available
                    ? "Dependency Sentinel cross-checked NuGet package metadata and GitHub Security Advisories."
                    : assessment.Error ?? "Security lookup failed.",
                EvidenceSanitizer.Clean(assessment.RegistryEvidence)));

            var decision = policy.EvaluateAdvisories(manifestDecision, assessment);
            store.Update(runId, run => run.AddStep(
                "confirm_advisory", decision.Allowed ? "ok" : "blocked", decision.Detail));
            if (!decision.Allowed || decision.Advisory is null || decision.PatchedManifest is null)
            {
                Block(runId, decision.Detail);
                return;
            }

            store.Update(runId, run => run.SetAdvisory(decision.Advisory.Id));
            await AddNarrationAsync(runId, decision.Advisory);
            store.Update(runId, run => run.AddStep(
                "manifest_diff", "ok", "The proposed change is a single direct dependency patch.",
                MaintenancePolicy.BuildDiff(manifest, decision.PatchedManifest)));

            store.Update(runId, run => run.Begin(MaintenanceRunStatuses.Sandboxing, "Patch & Verify: Nebius Sandbox"));
            var sandboxResult = await sandbox.RunApprovedPatchAsync(new MaintenanceSandboxRequest(
                fixtureRoot,
                MaintenancePolicy.FixtureProjectFileName,
                MaintenancePolicy.FixtureTestProjectFileName,
                manifest,
                decision.PatchedManifest,
                decision.Advisory.Id));

            store.Update(runId, run =>
            {
                foreach (var step in sandboxResult.Steps)
                {
                    run.AddStep(step.Name, step.Succeeded ? "ok" : "failed", step.Detail,
                        EvidenceSanitizer.Clean(step.Output));
                }
                run.SetSandboxSnapshot(sandboxResult.SnapshotId);
            });

            if (!sandboxResult.Succeeded)
            {
                Fail(runId, sandboxResult.Error ?? "Nebius Sandbox did not produce a verified remediation.");
                return;
            }

            store.Update(runId, run => run.Begin(MaintenanceRunStatuses.Testing, "Patch & Verify: tests passed"));
            store.Update(runId, run => run.Finish(
                MaintenanceRunStatuses.Remediated,
                "Sandbox-only remediation verified",
                null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = EvidenceSanitizer.Clean(ex.Message) ?? "Unexpected maintenance failure.";
            logger.LogError("Maintenance run {RunId} failed: {Error}", runId, error);
            Fail(runId, error);
        }
    }

    private async Task AddNarrationAsync(string runId, PackageSecurityAdvisory advisory)
    {
        try
        {
            var summary = await narrator.SummarizeAsync(advisory.Id, advisory.Severity, advisory.Summary);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                store.Update(runId, run =>
                {
                    run.SetSummary(summary);
                    run.AddStep("nvidia_risk_summary", "ok",
                        "NVIDIA model produced an evidence-grounded explanation.", summary);
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = EvidenceSanitizer.Clean(ex.Message);
            logger.LogWarning("NVIDIA maintenance narration failed for run {RunId}: {Error}", runId, error);
            store.Update(runId, run => run.AddStep("nvidia_risk_summary", "unavailable",
                "The remediation remains policy-verified; NVIDIA narration was unavailable.",
                error));
        }
    }

    private static string ResolveFixtureRoot(MaintenanceScenario scenario)
    {
        if (Path.IsPathRooted(scenario.RelativeFixtureRoot))
        {
            throw new InvalidOperationException("Maintenance scenario path must be relative to the deployed host binaries.");
        }

        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var root = Path.GetFullPath(Path.Combine(baseDirectory, scenario.RelativeFixtureRoot));
        if (!root.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Maintenance scenario path escapes the deployed host binaries.");
        }
        return root;
    }

    private void Block(string runId, string error) => store.Update(runId, run =>
        run.Finish(MaintenanceRunStatuses.Blocked, "No autonomous change made", error));

    private void Fail(string runId, string error) => store.Update(runId, run =>
        run.Finish(MaintenanceRunStatuses.Failed, "Sandbox verification failed", error));
}
