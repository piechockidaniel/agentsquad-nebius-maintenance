using AgentSquad.Agents.Maintenance;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace AgentSquad.NebiusSandbox;

/// <summary>Contree MCP adapter exposing the fixed v1 maintenance sequence only.</summary>
public sealed class ContreeMaintenanceSandbox(IConfiguration configuration) : IMaintenanceSandbox, IAsyncDisposable
{
    private const string DefaultImage = "docker://mcr.microsoft.com/dotnet/sdk:8.0";
    private const string ReusableImageTag = "agentsquad/maintenance/dotnet-sdk:8.0";
    private const string ReusableImageTagPrefix = "agentsquad/maintenance/";

    private static readonly IReadOnlyDictionary<string, string[]> RequiredTools =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["list_images"] = ["contree_list_images", "list_images"],
            ["import_image"] = ["contree_import_image", "import_image"],
            ["rsync"] = ["contree_rsync", "rsync"],
            ["upload"] = ["contree_upload", "upload"],
            ["run"] = ["contree_run", "run"]
        };

    private McpClient? _client;
    private HashSet<string> _tools = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _resolvedTools = new(StringComparer.OrdinalIgnoreCase);
    private string Command => configuration["NebiusSandbox:Command"] ?? (OperatingSystem.IsWindows()
        ? "contree-mcp.exe" : "contree-mcp");
    private string Image => configuration["NebiusSandbox:ImageRegistryUrl"]
        ?? DefaultImage;

    public async Task<MaintenanceSandboxPreflight> PreflightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Connect(cancellationToken);
            var missing = RequiredTools.Keys.Where(x => !_resolvedTools.ContainsKey(x)).ToArray();
            return missing.Length == 0 ? new(true, "Contree MCP connected to Nebius Token Factory Sandboxes.",
                _tools.Order().ToArray()) : new(false, $"Missing Sandbox tools: {string.Join(", ", missing)}.",
                _tools.ToArray());
        }
        catch (Exception ex)
        {
            return new(false, $"Sandbox preflight failed: " +
            $"{EvidenceSanitizer.Clean(ex.Message)}", []);
        }
    }

    public async Task<MaintenanceSandboxResult> RunApprovedPatchAsync(MaintenanceSandboxRequest request,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<MaintenanceSandboxStep>();
        string? stagedFixture = null;
        if (!(await PreflightAsync(cancellationToken)).Available) return new(false, null, steps,
            "Nebius Sandbox preflight failed.");
        try
        {
            var baseImage = await ResolveBaseImageAsync(cancellationToken);
            steps.Add(baseImage.Step);
            if (baseImage.Image is null)
                return new(false, null, steps, baseImage.Error);
            var image = baseImage.Image;

            string sync;
            IReadOnlyDictionary<string, string> fixtureFiles = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var staging = StageSourceFixture(request.FixtureRoot);
                stagedFixture = staging.Path;
                steps.Add(new("prepare_fixture", true,
                    $"Prepared {staging.FileCount} source files for the sandbox.", null));
                sync = await Call("rsync", new Dictionary<string, object?>
                {
                    ["source"] = stagedFixture,
                    ["destination"] = "/workspace"
                }, cancellationToken);
                fixtureFiles = await UploadFixtureFilesAsync(staging.Files, cancellationToken);
            }
            catch (Exception ex)
            {
                steps.Add(new("sync_fixture", false, "Sandbox fixture sync failed.",
                    EvidenceSanitizer.Clean(ex.Message)));
                return new(false, null, steps, "Sandbox fixture sync failed.");
            }

            var state = DirectoryStateId(sync);
            if (state is null)
            {
                steps.Add(new("sync_fixture", false, "Sandbox fixture sync did not return a directory state ID.",
                    Clip(sync)));
                return new(false, null, steps, "Sandbox fixture sync did not return a directory state ID.");
            }

            steps.Add(new("sync_fixture", true, "Synced the fixed maintenance fixture into the sandbox.", null));
            steps.Add(new("attach_fixture", true,
                "Attached the fixed source files at Linux container paths.", null));

            var baseline = await Run(VulnerabilityScanCommand,
                image, state.Value, fixtureFiles, true, cancellationToken);

            steps.Add(new("baseline_scan", Ok(baseline), "Scanned the unmodified fixture.", Clip(baseline)));

            if (!Ok(baseline))
                return new(false, null, steps, "Baseline vulnerability scan failed; no patch was staged.");

            var baselineTest = await Run(TestCommand, image, state.Value,
                fixtureFiles, true, cancellationToken);

            steps.Add(new("baseline_tests", Ok(baselineTest), "Verified that the unmodified scenario has a passing test baseline.",
                Clip(baselineTest)));

            if (!Ok(baselineTest))
                return new(false, null, steps, "Baseline tests failed; no patch was staged.");

            var upload = await Call("upload", new Dictionary<string, object?>
            {
                ["content"] = request.PatchedManifest
            }, cancellationToken);

            var patch = Value(upload, "uuid");

            if (patch is null)
                return new(false, null, steps, "Sandbox manifest staging failed.");

            var patchedFiles = new Dictionary<string, string>(fixtureFiles, StringComparer.Ordinal)
            {
                [$"/workspace/{request.ProjectFileName}"] = patch
            };
            var test = await Run(TestCommand, image, state.Value, patchedFiles, false,
                cancellationToken);

            steps.Add(new("run_tests", Ok(test), "Ran tests against the staged manifest patch.", Clip(test)));

            if (!Ok(test))
                return new(false, null, steps, "Sandbox tests failed.");

            var patchedImage = ResultImageId(test);
            if (patchedImage is null)
                return new(false, null, steps, "Sandbox tests did not retain a patch snapshot.");

            var rescan = await Run(VulnerabilityScanCommand, patchedImage, null, null, true, cancellationToken);
            var clean = Ok(rescan) && !rescan.Contains(request.AdvisoryId, StringComparison.OrdinalIgnoreCase);

            steps.Add(new("post_patch_scan", clean, "Confirmed the advisory is absent after the patch.", Clip(rescan)));

            return new(clean, patchedImage, steps,
                clean ? null : "Post-patch scan did not clear the advisory.");
        }
        catch (Exception ex)
        {
            return new(false, null, steps, $"Sandbox workflow failed: {EvidenceSanitizer.Clean(ex.Message)}");
        }
        finally
        {
            DeleteStagedFixture(stagedFixture);
        }
    }

    private async Task Connect(CancellationToken ct)
    {
        if (_client is not null)
            return;

        _client = await McpClient.CreateAsync(new StdioClientTransport(
            new()
            {
                Name = "nebius-sandbox",
                Command = Command
            }), cancellationToken: ct);

        _tools = (await _client.ListToolsAsync(cancellationToken: ct)).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _resolvedTools = RequiredTools
            .Select(pair => new { Operation = pair.Key, Tool = pair.Value.FirstOrDefault(_tools.Contains) })
            .Where(pair => pair.Tool is not null)
            .ToDictionary(pair => pair.Operation, pair => pair.Tool!, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string> Call(string name, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        if (!_resolvedTools.TryGetValue(name, out var toolName))
        {
            throw new InvalidOperationException($"Required Sandbox operation '{name}' is unavailable.");
        }

        var result = await _client!.CallToolAsync(toolName, args, cancellationToken: ct);
        var output = string.Join('\n', result.Content.OfType<TextContentBlock>().Select(x => x.Text));
        if (result.IsError == true)
            throw new InvalidOperationException($"Sandbox operation '{name}' failed: {output}");

        return output;
    }

    private async Task<BaseImageResolution> ResolveBaseImageAsync(CancellationToken ct)
    {
        try
        {
            var listed = await Call("list_images", new Dictionary<string, object?>
            {
                ["tag_prefix"] = ReusableImageTagPrefix,
                ["limit"] = 20
            }, ct);
            var existing = TaggedImageId(listed, ReusableImageTag);
            if (existing is not null)
            {
                return new(existing, new("resolve_base_image", true,
                    "Reused the approved .NET SDK sandbox image.", null), null);
            }
        }
        catch (Exception ex)
        {
            return FailedBaseImage("Sandbox base-image discovery failed.", ex.Message);
        }

        try
        {
            var imported = await Call("import_image", new Dictionary<string, object?>
            {
                ["registry_url"] = Image,
                ["tag"] = ReusableImageTag,
                ["wait"] = true,
                ["i_accept_that_anonymous_access_might_be_rate_limited"] = true
            }, ct);
            var image = ResultImageId(imported);
            return image is not null
                ? new(image, new("resolve_base_image", true,
                    "Imported and tagged the approved public .NET SDK sandbox image.", null), null)
                : FailedBaseImage("Sandbox base-image import did not return an image ID.", imported);
        }
        catch (Exception ex)
        {
            return FailedBaseImage("Sandbox base-image import failed.", ex.Message);
        }
    }

    private static BaseImageResolution FailedBaseImage(string error, string detail) => new(null,
        new("resolve_base_image", false, error, Clip(EvidenceSanitizer.Clean(detail) ?? string.Empty)), error);

    private static StagedFixture StageSourceFixture(string fixtureRoot)
    {
        var sourceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fixtureRoot));
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException("The configured maintenance fixture is unavailable.");

        var stagingParent = Path.Combine(Path.GetTempPath(), "agentsquad-maintenance");
        var stagingRoot = Path.Combine(stagingParent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Where(path => IsMaintenanceSourceFile(sourceRoot, path))
                .ToArray();
            if (sourceFiles.Length == 0)
                throw new InvalidOperationException("The maintenance fixture contains no permitted source files.");

            var sourcePrefix = sourceRoot + Path.DirectorySeparatorChar;
            var stagedFiles = new List<StagedFixtureFile>(sourceFiles.Length);
            foreach (var sourceFile in sourceFiles)
            {
                var fullSourcePath = Path.GetFullPath(sourceFile);
                if (!fullSourcePath.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A maintenance fixture file escapes its configured root.");

                var relativePath = Path.GetRelativePath(sourceRoot, fullSourcePath);
                var stagedPath = Path.Combine(stagingRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                File.Copy(fullSourcePath, stagedPath);
                stagedFiles.Add(new(stagedPath, "/workspace/" + relativePath.Replace('\\', '/')));
            }

            return new(stagingRoot, stagedFiles);
        }
        catch
        {
            DeleteStagedFixture(stagingRoot);
            throw;
        }
    }

    private static bool IsMaintenanceSourceFile(string sourceRoot, string path)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, path);
        var hasGeneratedDirectory = relativePath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(part, ".git", StringComparison.OrdinalIgnoreCase));

        return !hasGeneratedDirectory &&
               (string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyDictionary<string, string>> UploadFixtureFilesAsync(
        IReadOnlyList<StagedFixtureFile> stagedFiles, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stagedFile in stagedFiles)
        {
            var content = await File.ReadAllTextAsync(stagedFile.LocalPath, ct);
            var uploaded = await Call("upload", new Dictionary<string, object?> { ["content"] = content }, ct);
            var fileId = Value(uploaded, "uuid");
            if (fileId is null)
                throw new InvalidOperationException("Sandbox fixture-file staging did not return a file ID.");

            result[stagedFile.ContainerPath] = fileId;
        }

        return result;
    }

    private static void DeleteStagedFixture(string? stagingRoot)
    {
        if (string.IsNullOrWhiteSpace(stagingRoot))
            return;

        var stagingParent = Path.TrimEndingDirectorySeparator(Path.Combine(Path.GetTempPath(), "agentsquad-maintenance"))
            + Path.DirectorySeparatorChar;
        var fullStagingPath = Path.GetFullPath(stagingRoot);
        if (!fullStagingPath.StartsWith(stagingParent, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            Directory.Delete(fullStagingPath, recursive: true);
        }
        catch (IOException)
        {
            // A failed cleanup leaves only this run's isolated temporary fixture behind.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed cleanup leaves only this run's isolated temporary fixture behind.
        }
    }

    private Task<string> Run(string command, string image, long? state, IReadOnlyDictionary<string, string>? files,
        bool disposable, CancellationToken ct)
    {
        if (!IsApprovedCommand(command))
            throw new InvalidOperationException("The requested sandbox command is outside the fixed maintenance policy.");

        var a = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["image"] = image,
            ["shell"] = true,
            ["env"] = new Dictionary<string, string>
            {
                ["HOME"] = "/tmp/agentsquad",
                ["DOTNET_CLI_HOME"] = "/tmp/agentsquad"
            },
            ["cwd"] = "/workspace",
            ["wait"] = true,
            ["timeout"] = 120,
            ["files"] = files,
            ["disposable"] = disposable
        };
        if (state is not null)
            a["directory_state_id"] = state.Value;

        return Call("run", a, ct);
    }

    private static string VulnerabilityScanCommand =>
        $"dotnet restore {MaintenancePolicy.FixtureProjectFileName} && dotnet list " +
        $"{MaintenancePolicy.FixtureProjectFileName} package --vulnerable --include-transitive";

    private static string TestCommand => $"dotnet test {MaintenancePolicy.FixtureTestProjectFileName}";

    private static bool IsApprovedCommand(string command) =>
        string.Equals(command, VulnerabilityScanCommand, StringComparison.Ordinal) ||
        string.Equals(command, TestCommand, StringComparison.Ordinal);

    private static bool Ok(string output) =>
        output.Contains("\"exit_code\": 0", StringComparison.OrdinalIgnoreCase) && !output.Contains("\"timed_out\": true", StringComparison.OrdinalIgnoreCase);

    private static string? Value(string json, string name)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            return d.RootElement.TryGetProperty(name, out var v) ? v.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResultImageId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return String(document.RootElement, "result_image")
                ?? String(document.RootElement, "result_image_uuid")
                ?? (document.RootElement.TryGetProperty("result", out var result) ? String(result, "image") : null);
        }
        catch
        {
            return null;
        }
    }

    private static string? TaggedImageId(string json, string tag)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var image in images.EnumerateArray())
            {
                if (string.Equals(String(image, "tag"), tag, StringComparison.OrdinalIgnoreCase))
                    return String(image, "uuid");
            }
        }
        catch
        {
            // A malformed MCP payload is handled by importing the fixed approved image below.
        }

        return null;
    }

    private static long? DirectoryStateId(string text)
    {
        if (long.TryParse(text.Trim(), out var value))
            return value;

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Number && document.RootElement.TryGetInt64(out value))
                return value;

            return document.RootElement.TryGetProperty("directory_state_id", out var state) && state.TryGetInt64(out value)
                ? value
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Clip(string value) => value.Length <= 8000 ? value : value[..8000] + "\n[truncated]";

    public async ValueTask DisposeAsync() { if (_client is not null) await _client.DisposeAsync(); }

    private sealed record BaseImageResolution(string? Image, MaintenanceSandboxStep Step, string? Error);

    private sealed record StagedFixture(string Path, IReadOnlyList<StagedFixtureFile> Files)
    {
        public int FileCount => Files.Count;
    }

    private sealed record StagedFixtureFile(string LocalPath, string ContainerPath);
}
