using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentSquad.Agents.Maintenance;

public sealed class TokenFactoryNarrator(IHttpClientFactory clients, IOptions<AgentSquadOptions> options)
    : IMaintenanceNarrator
{
    public async Task<string?> SummarizeAsync(string advisoryId, string severity, string summary,
        CancellationToken cancellationToken = default)
    {
        var key = options.Value.TokenFactory.ApiKey ??
            Environment.GetEnvironmentVariable("NEBIUS_TOKEN_FACTORY_API_KEY");

        if (string.IsNullOrWhiteSpace(key)) return null;

        var client = clients.CreateClient("TokenFactory");
        var body = new
        {
            model = options.Value.Maintenance.ModelId,
            messages = new[]
            {
                new {
                    role = "system",
                    content = "State the confirmed dependency risk and exact sandbox-only repair in two short sentences. " +
                    "Do not invent facts." },
                new {
                    role = "assistant",
                    content = "Understood. Summarizing now." },
                new {
                    role= "user",
                    content = $"{advisoryId}; severity {severity}; {summary}; " +
                    $"repair: Microsoft.Extensions.Caching.Memory 8.0.0 to 8.0.1." }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tokenfactory.nebius.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var response = await client.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return EvidenceSanitizer.Clean(document.RootElement.GetProperty("choices")[0].GetProperty("message")
            .GetProperty("content").GetString());
    }
}
