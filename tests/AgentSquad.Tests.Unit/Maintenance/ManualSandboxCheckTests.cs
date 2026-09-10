using AgentSquad.Agents.Configuration;
using AgentSquad.Agents.Maintenance;
using Microsoft.Extensions.Options;

namespace AgentSquad.Tests.Unit.Maintenance;

public sealed class ManualSandboxCheckTests
{
    [Fact]
    public void Exposes_only_fixed_loopback_checks()
    {
        ManualSandboxCheckCatalog.All.Select(check => check.Id).Should().BeEquivalentTo(
            ["health", "work-item-42"]);

        ManualSandboxCheckCatalog.All.Should().OnlyContain(check =>
            check.Command.Contains("http://127.0.0.1:8080/", StringComparison.Ordinal) &&
            !check.Command.Contains("https://", StringComparison.Ordinal) &&
            check.Command.Contains("trap cleanup EXIT", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_manual_checks_until_a_run_is_remediated_with_a_snapshot()
    {
        var store = new MaintenanceRunStore(Options.Create(new AgentSquadOptions()));
        var run = store.TryStart(MaintenanceRunTriggers.Manual, "cache-repair")!;
        ManualSandboxCheckCatalog.TryGet("health", out var check).Should().BeTrue();

        var decision = store.TryStartManualCheck(run.Id, check);

        decision.Started.Should().BeFalse();
        decision.StatusCode.Should().Be(409);
    }

    [Fact]
    public void Allows_only_one_manual_sandbox_operation_at_a_time()
    {
        var store = new MaintenanceRunStore(Options.Create(new AgentSquadOptions()));
        var run = store.TryStart(MaintenanceRunTriggers.Manual, "cache-repair")!;
        store.Update(run.Id, record =>
        {
            record.SetSandboxSnapshot("sandbox-snapshot");
            record.Finish(MaintenanceRunStatuses.Remediated, "verified");
        });
        ManualSandboxCheckCatalog.TryGet("health", out var health).Should().BeTrue();
        ManualSandboxCheckCatalog.TryGet("work-item-42", out var workItem).Should().BeTrue();

        var first = store.TryStartManualCheck(run.Id, health);
        var second = store.TryStartManualCheck(run.Id, workItem);

        first.Started.Should().BeTrue();
        second.Started.Should().BeFalse();
        second.StatusCode.Should().Be(409);
        store.TryStart(MaintenanceRunTriggers.Manual, "cache-repair").Should().BeNull();

        store.UpdateManualCheck(run.Id, first.Check!.Id, check => check.Finish(true, "ok"));

        store.TryStartManualCheck(run.Id, workItem).Started.Should().BeTrue();
    }

    [Fact]
    public void Keeps_manual_check_evidence_separate_from_remediation_status()
    {
        var store = new MaintenanceRunStore(Options.Create(new AgentSquadOptions()));
        var run = store.TryStart(MaintenanceRunTriggers.Manual, "cache-repair")!;
        store.Update(run.Id, record =>
        {
            record.SetSandboxSnapshot("sandbox-snapshot");
            record.Finish(MaintenanceRunStatuses.Remediated, "verified");
        });
        ManualSandboxCheckCatalog.TryGet("health", out var health).Should().BeTrue();

        var started = store.TryStartManualCheck(run.Id, health);
        store.UpdateManualCheck(run.Id, started.Check!.Id, check => check.Finish(false, null, "response mismatch"));
        var snapshot = store.Get(run.Id)!;

        snapshot.Status.Should().Be(MaintenanceRunStatuses.Remediated);
        snapshot.ManualChecks.Should().ContainSingle().Which.Status.Should().Be(ManualSandboxCheckStatuses.Failed);
        snapshot.ManualChecks.Single().Error.Should().Be("response mismatch");
    }
}
