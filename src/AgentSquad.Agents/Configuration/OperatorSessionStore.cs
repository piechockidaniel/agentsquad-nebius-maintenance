using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSquad.Agents.Configuration;

/// <summary>
/// Keeps short-lived, opaque production operator sessions. When configured with a protected
/// server-side file, only token hashes survive a host restart; credentials never do.
/// </summary>
public sealed class OperatorSessionStore
{
    public const string CookieName = "agentsquad_operator";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private const int MaximumActiveSessions = 20;

    private readonly Dictionary<string, DateTimeOffset> _expiresAt = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly string? _sessionStorePath;
    private readonly ILogger<OperatorSessionStore>? _logger;

    /// <summary>Creates an in-memory session store, primarily for local development and tests.</summary>
    public OperatorSessionStore()
    {
    }

    public OperatorSessionStore(IOptions<AgentSquadOptions> options, ILogger<OperatorSessionStore> logger)
        : this(options.Value.Web.SessionStorePath, logger)
    {
    }

    public OperatorSessionStore(string? sessionStorePath)
        : this(sessionStorePath, null)
    {
    }

    private OperatorSessionStore(string? sessionStorePath, ILogger<OperatorSessionStore>? logger)
    {
        _sessionStorePath = string.IsNullOrWhiteSpace(sessionStorePath) ? null : Path.GetFullPath(sessionStorePath);
        _logger = logger;
        Load(DateTimeOffset.UtcNow);
    }

    public string Create(DateTimeOffset now)
    {
        lock (_gate)
        {
            RemoveExpiredLocked(now);

            if (_expiresAt.Count >= MaximumActiveSessions)
            {
                foreach (var entry in _expiresAt.OrderBy(entry => entry.Value).Take(_expiresAt.Count - MaximumActiveSessions + 1))
                {
                    _expiresAt.Remove(entry.Key);
                }
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                var tokenHash = Hash(token);
                if (_expiresAt.TryAdd(tokenHash, now.Add(SessionLifetime)))
                {
                    PersistLocked();
                    return token;
                }
            }

            throw new InvalidOperationException("Could not create an operator session.");
        }
    }

    public bool IsActive(string? token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        lock (_gate)
        {
            var tokenHash = Hash(token);
            if (!_expiresAt.TryGetValue(tokenHash, out var expiresAt))
            {
                return false;
            }

            if (expiresAt > now)
            {
                return true;
            }

            _expiresAt.Remove(tokenHash);
            PersistLocked();
            return false;
        }
    }

    public void Revoke(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        lock (_gate)
        {
            if (_expiresAt.Remove(Hash(token)))
            {
                PersistLocked();
            }
        }
    }

    private void Load(DateTimeOffset now)
    {
        if (_sessionStorePath is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_sessionStorePath)!;
            Directory.CreateDirectory(directory);
            SetDirectoryPermissions(directory);

            if (!File.Exists(_sessionStorePath))
            {
                return;
            }

            var sessions = JsonSerializer.Deserialize<List<PersistedSession>>(File.ReadAllText(_sessionStorePath)) ?? [];
            foreach (var session in sessions.Where(session => IsHash(session.TokenHash) && session.ExpiresAt > now)
                         .OrderByDescending(session => session.ExpiresAt)
                         .Take(MaximumActiveSessions))
            {
                _expiresAt.TryAdd(session.TokenHash, session.ExpiresAt);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.LogWarning("Operator session persistence could not be loaded; new sessions will remain available until this host restarts.");
        }
    }

    private void RemoveExpiredLocked(DateTimeOffset now)
    {
        foreach (var tokenHash in _expiresAt.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToList())
        {
            _expiresAt.Remove(tokenHash);
        }
    }

    private void PersistLocked()
    {
        if (_sessionStorePath is null)
        {
            return;
        }

        string? temporaryPath = null;
        try
        {
            var directory = Path.GetDirectoryName(_sessionStorePath)!;
            Directory.CreateDirectory(directory);
            SetDirectoryPermissions(directory);

            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_sessionStorePath)}.{Guid.NewGuid():N}.tmp");
            var sessions = _expiresAt.Select(entry => new PersistedSession(entry.Key, entry.Value)).ToArray();
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, sessions);
                stream.Flush(flushToDisk: true);
            }

            SetFilePermissions(temporaryPath);
            File.Move(temporaryPath, _sessionStorePath, overwrite: true);
            SetFilePermissions(_sessionStorePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning("Operator session persistence could not be updated; existing sessions remain available until this host restarts.");
        }
        finally
        {
            try
            {
                if (temporaryPath is not null && File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger?.LogWarning("An incomplete operator session persistence write could not be removed.");
            }
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static bool IsHash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void SetDirectoryPermissions(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void SetFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed record PersistedSession(string TokenHash, DateTimeOffset ExpiresAt);
}
