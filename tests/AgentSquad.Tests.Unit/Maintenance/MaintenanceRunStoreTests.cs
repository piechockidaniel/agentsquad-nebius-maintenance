using AgentSquad.Agents.Configuration;
using AgentSquad.Agents.Maintenance;
using Microsoft.Extensions.Options;

namespace AgentSquad.Tests.Unit.Maintenance;

public sealed class MaintenanceRunStoreTests
{
    [Fact]
    public void Rejects_a_second_active_run_then_accepts_one_after_terminal_state()
    {
        var store = new MaintenanceRunStore(Options.Create(new AgentSquadOptions()));
        var first = store.TryStart(MaintenanceRunTriggers.Manual)!;

        store.TryStart(MaintenanceRunTriggers.Manual).Should().BeNull();

        store.Update(first.Id, run => run.Finish(MaintenanceRunStatuses.Blocked, "blocked"));
        store.TryStart(MaintenanceRunTriggers.Manual).Should().NotBeNull();
    }
}
