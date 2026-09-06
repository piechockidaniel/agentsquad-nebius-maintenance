using System.ComponentModel.DataAnnotations;

namespace AgentSquad.Agents.Configuration;

public sealed class AgentSquadOptions
{
    public const string SectionName = "AgentSquad";
    public TokenFactoryOptions TokenFactory { get; set; } = new();
    public MaintenanceOptions Maintenance { get; set; } = new();
    public WebOptions Web { get; set; } = new();
}

public sealed class TokenFactoryOptions
{
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
    /// Required outside Development before maintenance evidence or a repair run can be requested.
    /// It protects the public Container VM URL without placing a credential in the static console.
    /// </summary>
    [MinLength(32)] public string? AccessToken { get; set; }
}
