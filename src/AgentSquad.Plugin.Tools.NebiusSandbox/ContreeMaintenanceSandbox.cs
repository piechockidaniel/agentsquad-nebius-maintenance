using AgentSquad.Agents.Maintenance;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace AgentSquad.NebiusSandbox;

/// <summary>Contree MCP adapter exposing the fixed v1 maintenance sequence only.</summary>
public sealed class ContreeMaintenanceSandbox(IConfiguration configuration) : IMaintenanceSandbox, IAsyncDisposable
{
    private static readonly string[] Required = ["list_images", "import_image", "rsync", "upload", "run"];
    private McpClient? _client;
    private HashSet<string> _tools = new(StringComparer.OrdinalIgnoreCase);
    private string Command => configuration["NebiusSandbox:Command"] ?? (OperatingSystem.IsWindows() ? "contree-mcp.exe" : "contree-mcp");
    private string Image => configuration["NebiusSandbox:ImageRegistryUrl"] ?? "docker://mcr.microsoft.com/dotnet/sdk:8.0";

    public async Task<MaintenanceSandboxPreflight> PreflightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Connect(cancellationToken);
            var missing = Required.Where(x => !_tools.Contains(x)).ToArray();
            return missing.Length == 0 ? new(true, "Contree MCP connected to Nebius Token Factory Sandboxes.", _tools.Order().ToArray()) : new(false, $"Missing Sandbox tools: {string.Join(", ", missing)}.", _tools.ToArray());
        }
        catch (Exception ex) { return new(false, $"Sandbox preflight failed: {EvidenceSanitizer.Clean(ex.Message)}", []); }
    }

    public async Task<MaintenanceSandboxResult> RunApprovedPatchAsync(MaintenanceSandboxRequest request, CancellationToken cancellationToken = default)
    {
        var steps = new List<MaintenanceSandboxStep>();
        if (!(await PreflightAsync(cancellationToken)).Available) return new(false, null, steps, "Nebius Sandbox preflight failed.");
        try
        {
            var imported = await Call("import_image", new Dictionary<string, object?> { ["registry_url"] = Image }, cancellationToken);
            var image = Value(imported, "result_image");
            var sync = await Call("rsync", new Dictionary<string, object?> { ["source"] = request.FixtureRoot, ["destination"] = "/workspace", ["exclude"] = new[] { ".git", "bin", "obj" } }, cancellationToken);
            var state = Number(sync);
            if (image is null || state is null) return new(false, null, steps, "Sandbox image or fixture sync failed.");
            var baseline = await Run($"dotnet list {request.ProjectFileName} package --vulnerable --include-transitive", image, state.Value, null, true, cancellationToken);
            steps.Add(new("baseline_scan", Ok(baseline), "Scanned the unmodified fixture.", Clip(baseline)));
            var upload = await Call("upload", new Dictionary<string, object?> { ["content"] = request.PatchedManifest }, cancellationToken);
            var patch = Value(upload, "uuid");
            if (patch is null) return new(false, null, steps, "Sandbox manifest staging failed.");
            var files = new Dictionary<string, string> { [$"/workspace/{request.ProjectFileName}"] = patch };
            var test = await Run($"dotnet test {request.TestProjectFileName}", image, state.Value, files, false, cancellationToken);
            steps.Add(new("run_tests", Ok(test), "Ran tests against the staged manifest patch.", Clip(test)));
            if (!Ok(test)) return new(false, null, steps, "Sandbox tests failed.");
            var rescan = await Run($"dotnet list {request.ProjectFileName} package --vulnerable --include-transitive", image, state.Value, files, false, cancellationToken);
            var clean = Ok(rescan) && !rescan.Contains(request.AdvisoryId, StringComparison.OrdinalIgnoreCase);
            steps.Add(new("post_patch_scan", clean, "Confirmed the advisory is absent after the patch.", Clip(rescan)));
            return new(clean, Value(rescan, "result_image"), steps, clean ? null : "Post-patch scan did not clear the advisory.");
        }
        catch (Exception ex) { return new(false, null, steps, $"Sandbox workflow failed: {EvidenceSanitizer.Clean(ex.Message)}"); }
    }

    private async Task Connect(CancellationToken ct) { if (_client is not null) return; _client = await McpClient.CreateAsync(new StdioClientTransport(new() { Name = "nebius-sandbox", Command = Command }), cancellationToken: ct); _tools = (await _client.ListToolsAsync(cancellationToken: ct)).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase); }
    private async Task<string> Call(string name, IReadOnlyDictionary<string, object?> args, CancellationToken ct) => string.Join('\n', (await _client!.CallToolAsync(name, args, cancellationToken: ct)).Content.OfType<TextContentBlock>().Select(x => x.Text));
    private Task<string> Run(string command, string image, long state, IReadOnlyDictionary<string, string>? files, bool disposable, CancellationToken ct) { var a = new Dictionary<string, object?> { ["command"] = command, ["image"] = image, ["shell"] = false, ["directory_state_id"] = state, ["cwd"] = "/workspace", ["wait"] = true, ["timeout"] = 120, ["files"] = files, ["disposable"] = disposable }; return Call("run", a, ct); }
    private static bool Ok(string output) => output.Contains("\"exit_code\": 0", StringComparison.OrdinalIgnoreCase) && !output.Contains("\"timed_out\": true", StringComparison.OrdinalIgnoreCase);
    private static string? Value(string json, string name) { try { using var d = JsonDocument.Parse(json); return d.RootElement.TryGetProperty(name, out var v) ? v.GetString() : null; } catch { return null; } }
    private static long? Number(string text) => long.TryParse(text.Trim(), out var value) ? value : null;
    private static string Clip(string value) => value.Length <= 8000 ? value : value[..8000] + "\n[truncated]";
    public async ValueTask DisposeAsync() { if (_client is not null) await _client.DisposeAsync(); }
}
