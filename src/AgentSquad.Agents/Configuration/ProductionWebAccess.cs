using System.Security.Cryptography;
using System.Text;

namespace AgentSquad.Agents.Configuration;

/// <summary>
/// Validates the operator credentials used to create a production web-console session.
/// Provider credentials never participate in, or leave, this boundary.
/// </summary>
public static class ProductionWebAccess
{
    public static bool IsOperatorConfigured(WebOptions options) =>
        !string.IsNullOrWhiteSpace(options.OperatorUsername) &&
        !string.IsNullOrWhiteSpace(options.OperatorPassword);

    public static bool AreOperatorCredentialsValid(string? username, string? password, WebOptions options)
    {
        if (!IsOperatorConfigured(options) || username is null || password is null)
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        var expectedBytes = Encoding.UTF8.GetBytes($"{options.OperatorUsername}:{options.OperatorPassword}");

        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
