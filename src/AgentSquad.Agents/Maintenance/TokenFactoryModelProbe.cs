using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AgentSquad.Agents.Maintenance;

public sealed class TokenFactoryModelProbe(IHttpClientFactory clients, IOptions<AgentSquadOptions> options)
    : IMaintenanceModelProbe
{
    public async Task<MaintenanceModelPreflight> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var key = options.Value.TokenFactory.ApiKey
            ?? Environment.GetEnvironmentVariable("NEBIUS_TOKEN_FACTORY_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return new(false, "NEBIUS_TOKEN_FACTORY_API_KEY is not configured.");
        try
        {
            var client = clients.CreateClient("TokenFactory");

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.tokenfactory.nebius.com/v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode) return new(false,
                $"Token Factory model listing returned HTTP {(int)response.StatusCode}.");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var found = document.RootElement.GetProperty("data").EnumerateArray()
                .Any(x => x.GetProperty("id").GetString() == options.Value.Maintenance.ModelId);
            return found ? new(true, $"Token Factory exposes {options.Value.Maintenance.ModelId}.")
                : new(false, "Configured NVIDIA model is unavailable to this Token Factory key.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return new(false, EvidenceSanitizer.Clean(ex.Message)!);
        }
    }
}
