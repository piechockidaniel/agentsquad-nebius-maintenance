using System.ComponentModel.DataAnnotations;

namespace AgentSquad.Agents.Configuration;

public sealed class AgentSquadOptions
{
    public const string SectionName = "AgentSquad";
    public TokenFactoryOptions TokenFactory { get; set; } = new();
    public TavilyOptions Tavily { get; set; } = new();
    public MaintenanceOptions Maintenance { get; set; } = new();
    public WebOptions Web { get; set; } = new();
}

public sealed class TokenFactoryOptions
{
    public string? ApiKey { get; set; }
}

public sealed class TavilyOptions
{
    /// <summary>Optional supplementary security-research key. It never expands repair policy.</summary>
    public string? ApiKey { get; set; }
}

public sealed class MaintenanceOptions
{
    public bool Enabled { get; set; }
    public string DefaultScenarioId { get; set; } = "cache-repair";
    public string ModelId { get; set; } = "nvidia/nemotron-3-super-120b-a12b";
    [Range(1, 200)] public int HistoryLimit { get; set; } = 50;
    public string ScheduleCron { get; set; } = "30 7 * * 1-5";
}

public sealed class WebOptions
{
    /// <summary>
    /// Required outside Development to view the public console. The values are
    /// supplied only through the deployment environment and never reach the browser bundle.
    /// </summary>
    [MinLength(3)] public string? OperatorUsername { get; set; }
    [MinLength(20)] public string? OperatorPassword { get; set; }

    /// <summary>
    /// Required outside Development before maintenance evidence or a repair run can be requested.
    /// This is a second, distinct control after operator authentication; it is never a provider credential.
    /// </summary>
    [MinLength(32)] public string? AccessToken { get; set; }

    /// <summary>
    /// Optional server-only file used to retain hashes of active operator sessions across a host restart.
    /// This is deliberately not a browser storage setting and must be on a protected volume.
    /// </summary>
    public string? SessionStorePath { get; set; }
}
