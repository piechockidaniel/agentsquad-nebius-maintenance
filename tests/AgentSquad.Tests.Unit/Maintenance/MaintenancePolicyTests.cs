using AgentSquad.Agents.Maintenance;

namespace AgentSquad.Tests.Unit.Maintenance;

public sealed class MaintenancePolicyTests
{
    private const string VulnerableManifest = """
        <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
          <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
        </ItemGroup></Project>
        """;

    [Fact]
    public void Allows_only_the_exact_direct_patch()
    {
        var result = new MaintenancePolicy().EvaluateManifest(VulnerableManifest);

        result.Allowed.Should().BeTrue();
        result.PatchedManifest.Should().Contain("Version=\"8.0.1\"");
    }

    [Theory]
    [InlineData("8.0.2")]
    [InlineData("9.0.0")]
    public void Blocks_a_version_outside_the_single_approved_input(string version)
    {
        var manifest = VulnerableManifest.Replace("8.0.0", version, StringComparison.Ordinal);

        new MaintenancePolicy().EvaluateManifest(manifest).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Requires_the_confirmed_advisory_and_fixed_version()
    {
        var policy = new MaintenancePolicy();
        var manifest = policy.EvaluateManifest(VulnerableManifest);
        var assessment = new PackageSecurityAssessment(true,
            [new PackageSecurityAdvisory("GHSA-qj66-m88j-hmgj", "summary", "High", "<= 8.0.0", "8.0.1")], "NuGet: ok");

        var result = policy.EvaluateAdvisories(manifest, assessment);

        result.Allowed.Should().BeTrue();
        result.Advisory!.Id.Should().Be("GHSA-qj66-m88j-hmgj");
    }
}
