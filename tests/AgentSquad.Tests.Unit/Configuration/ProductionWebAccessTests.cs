using AgentSquad.Agents.Configuration;
using System.Text;

namespace AgentSquad.Tests.Unit.Configuration;

public sealed class ProductionWebAccessTests
{
    private static readonly WebOptions Options = new()
    {
        OperatorUsername = "operator",
        OperatorPassword = "correct-horse-battery-staple"
    };

    [Fact]
    public void Accepts_the_exact_basic_operator_credentials()
    {
        ProductionWebAccess.IsOperatorAuthorized(Basic("operator:correct-horse-battery-staple"), Options)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer any-token")]
    [InlineData("Basic not-base64")]
    public void Rejects_missing_or_malformed_authorization(string? authorization)
    {
        ProductionWebAccess.IsOperatorAuthorized(authorization, Options).Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_incorrect_operator_password()
    {
        ProductionWebAccess.IsOperatorAuthorized(Basic("operator:wrong-password-which-is-long"), Options)
            .Should().BeFalse();
    }

    [Fact]
    public void Rejects_requests_when_operator_configuration_is_incomplete()
    {
        var incomplete = new WebOptions { OperatorUsername = "operator" };

        ProductionWebAccess.IsOperatorConfigured(incomplete).Should().BeFalse();
        ProductionWebAccess.IsOperatorAuthorized(Basic("operator:anything"), incomplete).Should().BeFalse();
    }

    private static string Basic(string value) =>
        $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}";
}
