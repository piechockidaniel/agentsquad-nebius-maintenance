using Xunit;

namespace MaintenanceLab.Tests;

public sealed class KnownBaselineRegressionTests
{
    [Fact]
    public void Demonstrates_a_preexisting_regression() =>
        Assert.Fail("Intentional demo scenario: the baseline must be green before AgentSquad can stage a dependency repair.");
}
