using System.Security.Cryptography;
using System.Text;

namespace AgentSquad.Agents.Configuration;

/// <summary>
/// Validates the Basic operator challenge used in front of the production web console.
/// Provider credentials never participate in, or leave, this boundary.
/// </summary>
public static class ProductionWebAccess
{
    public const string AuthenticationScheme = "Basic";
    public const string AuthenticationChallenge = "Basic realm=\"AgentSquad operator\", charset=\"UTF-8\"";

    public static bool IsOperatorConfigured(WebOptions options) =>
        !string.IsNullOrWhiteSpace(options.OperatorUsername) &&
        !string.IsNullOrWhiteSpace(options.OperatorPassword);

    public static bool IsOperatorAuthorized(string? authorizationHeader, WebOptions options)
    {
        if (!IsOperatorConfigured(options) || string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        var prefix = $"{AuthenticationScheme} ";
        if (!authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string supplied;
        try
        {
            supplied = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader[prefix.Length..]));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = $"{options.OperatorUsername}:{options.OperatorPassword}";
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
