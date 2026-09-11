using AgentSquad.Agents.Configuration;

namespace AgentSquad.Tests.Unit.Configuration;

public sealed class OperatorSessionStoreTests
{
    [Fact]
    public void Keeps_a_new_session_active_until_its_expiry()
    {
        var now = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);
        var store = new OperatorSessionStore();

        var session = store.Create(now);

        store.IsActive(session, now.Add(OperatorSessionStore.SessionLifetime).AddSeconds(-1)).Should().BeTrue();
        store.IsActive(session, now.Add(OperatorSessionStore.SessionLifetime)).Should().BeFalse();
    }

    [Fact]
    public void Revokes_a_session_explicitly()
    {
        var store = new OperatorSessionStore();
        var session = store.Create(DateTimeOffset.UtcNow);

        store.Revoke(session);

        store.IsActive(session, DateTimeOffset.UtcNow).Should().BeFalse();
    }
}
