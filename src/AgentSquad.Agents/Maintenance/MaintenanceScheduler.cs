using AgentSquad.Agents.Configuration;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgentSquad.Agents.Maintenance;

public sealed class MaintenanceScheduler(MaintenanceService maintenance, IOptions<AgentSquadOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Maintenance.Enabled) return;
        var cron = CronExpression.Parse(options.Value.Maintenance.ScheduleCron);
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
            if (next is null) return;
            await Task.Delay(next.Value - DateTimeOffset.UtcNow, stoppingToken);
            maintenance.Start(MaintenanceRunTriggers.Scheduled);
        }
    }
}
