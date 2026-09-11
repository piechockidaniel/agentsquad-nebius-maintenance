using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AgentSquad.Agents.Configuration;

/// <summary>
/// Keeps short-lived, opaque production operator sessions in process memory.
/// Restarting the host intentionally invalidates every session.
/// </summary>
public sealed class OperatorSessionStore
{
    public const string CookieName = "agentsquad_operator";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private const int MaximumActiveSessions = 20;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _expiresAt = new(StringComparer.Ordinal);

    public string Create(DateTimeOffset now)
    {
        RemoveExpired(now);

        if (_expiresAt.Count >= MaximumActiveSessions)
        {
            foreach (var entry in _expiresAt.OrderBy(entry => entry.Value).Take(_expiresAt.Count - MaximumActiveSessions + 1))
            {
                _expiresAt.TryRemove(entry.Key, out _);
            }
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            if (_expiresAt.TryAdd(token, now.Add(SessionLifetime)))
            {
                return token;
            }
        }

        throw new InvalidOperationException("Could not create an operator session.");
    }

    public bool IsActive(string? token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) || !_expiresAt.TryGetValue(token, out var expiresAt))
        {
            return false;
        }

        if (expiresAt > now)
        {
            return true;
        }

        _expiresAt.TryRemove(token, out _);
        return false;
    }

    public void Revoke(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _expiresAt.TryRemove(token, out _);
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in _expiresAt.Where(entry => entry.Value <= now))
        {
            _expiresAt.TryRemove(entry.Key, out _);
        }
    }
}
