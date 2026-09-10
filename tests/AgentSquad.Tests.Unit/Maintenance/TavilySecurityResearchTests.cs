using AgentSquad.Agents.Configuration;
using AgentSquad.Agents.Maintenance;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace AgentSquad.Tests.Unit.Maintenance;

public sealed class TavilySecurityResearchTests
{
    private static readonly PackageSecurityAdvisory Advisory = new(
        MaintenancePolicy.AdvisoryId, "Known denial of service risk.", "high", "<= 8.0.0", "8.0.1");
    private static readonly string TestApiKey = string.Concat("tvly-", "test-key-not-real");

    [Fact]
    public async Task Sends_a_bounded_search_and_keeps_only_approved_https_sources()
    {
        var handler = new StubHandler(async request =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.ToString().Should().Be("https://api.tavily.com/search");
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be(TestApiKey);
            (await request.Content!.ReadAsStringAsync()).Should().Contain(MaintenancePolicy.AdvisoryId);

            return Json(HttpStatusCode.OK, """
                {
                  "request_id": "request-123",
                  "usage": { "credits": 1 },
                  "results": [
                    { "title": "GitHub advisory", "url": "https://github.com/advisories/GHSA-qj66-m88j-hmgj", "content": "The advisory." },
                    { "title": "NuGet package", "url": "https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory", "content": "Package information." },
                    { "title": "Microsoft guidance", "url": "https://learn.microsoft.com/dotnet", "content": "Guidance." },
                    { "title": "Ignored host", "url": "https://example.test/advisory", "content": "Ignore me." }
                  ]
                }
                """);
        });
        var research = new TavilySecurityResearch(new StubClientFactory(handler), Options.Create(new AgentSquadOptions
        {
            Tavily = new TavilyOptions { ApiKey = TestApiKey }
        }));

        var result = await research.ResearchAsync(Advisory, TestContext.Current.CancellationToken);

        result.Available.Should().BeTrue();
        result.RequestId.Should().Be("request-123");
        result.CreditsUsed.Should().Be(1);
        result.Sources.Should().HaveCount(3);
        result.Sources.Select(source => source.Url).Should().OnlyContain(url => !url.Contains("example.test"));
    }

    [Fact]
    public async Task Does_not_make_a_web_request_without_a_configured_key()
    {
        var research = new TavilySecurityResearch(new StubClientFactory(new ThrowingHandler()), Options.Create(new AgentSquadOptions
        {
            Tavily = new TavilyOptions { ApiKey = string.Empty }
        }));

        var result = await research.ResearchAsync(Advisory, TestContext.Current.CancellationToken);

        result.Available.Should().BeFalse();
        result.Detail.Should().Contain("not configured");
    }

    [Fact]
    public void Redacts_a_tavily_key_from_evidence()
    {
        var simulatedKey = string.Concat("tvly-", new string('x', 26));

        EvidenceSanitizer.Clean($"Tavily token {simulatedKey}").Should()
            .Be("Tavily token [redacted]");
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handle(request);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("Tavily must not be called without a configured key.");
    }
}
