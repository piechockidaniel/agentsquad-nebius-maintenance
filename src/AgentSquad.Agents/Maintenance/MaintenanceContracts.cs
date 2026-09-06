namespace AgentSquad.Agents.Maintenance;

public interface IPackageSecurityLookup
{
    Task<PackageSecurityAssessment> InspectAsync(string ecosystem, string packageName, string version, CancellationToken cancellationToken = default);
}

public sealed record PackageSecurityAdvisory(string Id, string Summary, string Severity, string VulnerableVersionRange, string? FirstPatchedVersion, string? Url = null);
public sealed record PackageSecurityAssessment(bool Available, IReadOnlyList<PackageSecurityAdvisory> Advisories, string? RegistryEvidence = null, string? Error = null);

public interface IMaintenanceSandbox
{
    Task<MaintenanceSandboxPreflight> PreflightAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceSandboxResult> RunApprovedPatchAsync(MaintenanceSandboxRequest request, CancellationToken cancellationToken = default);
}

public sealed record MaintenanceSandboxPreflight(bool Available, string Detail, IReadOnlyList<string> AvailableTools);
public sealed record MaintenanceSandboxRequest(string FixtureRoot, string ProjectFileName, string TestProjectFileName, string OriginalManifest, string PatchedManifest, string AdvisoryId);
public sealed record MaintenanceSandboxStep(string Name, bool Succeeded, string Detail, string? Output = null);
public sealed record MaintenanceSandboxResult(bool Succeeded, string? SnapshotId, IReadOnlyList<MaintenanceSandboxStep> Steps, string? Error = null);
