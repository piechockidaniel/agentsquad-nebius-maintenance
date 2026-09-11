using AgentSquad.Agents.Configuration;
namespace AgentSquad.Tests.Unit.Configuration;

public sealed class ProductionWebAccessTests
{
    private static readonly WebOptions Options = new()
    {
        OperatorUsername = "operator",
        OperatorPassword = "correct-horse-battery-staple"
    };

    [Fact]
    public void Accepts_the_exact_operator_credentials()
    {
        ProductionWebAccess.AreOperatorCredentialsValid("operator", "correct-horse-battery-staple", Options)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("somebody")]
    public void Rejects_missing_or_incorrect_operator_username(string? username)
    {
        ProductionWebAccess.AreOperatorCredentialsValid(username, "correct-horse-battery-staple", Options).Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_incorrect_operator_password()
    {
        ProductionWebAccess.AreOperatorCredentialsValid("operator", "wrong-password-which-is-long", Options)
            .Should().BeFalse();
    }

    [Fact]
    public void Rejects_requests_when_operator_configuration_is_incomplete()
    {
        var incomplete = new WebOptions { OperatorUsername = "operator" };

        ProductionWebAccess.IsOperatorConfigured(incomplete).Should().BeFalse();
        ProductionWebAccess.AreOperatorCredentialsValid("operator", "anything", incomplete).Should().BeFalse();
    }
}
