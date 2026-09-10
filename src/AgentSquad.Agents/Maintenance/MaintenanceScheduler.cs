using AgentSquad.Agents.Configuration;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgentSquad.Agents.Maintenance;

public sealed class MaintenanceScheduler(MaintenanceService maintenance, IOptions<AgentSquadOptions> options)
    : BackgroundService
{
    // Task.Delay cannot accept a timer interval longer than Int32.MaxValue milliseconds.
    // Long cron intervals are therefore waited in chunks and then calculated again.
    private static readonly TimeSpan MaximumTimerDelay = TimeSpan.FromMilliseconds(int.MaxValue - 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Maintenance.Enabled) return;
        var cron = CronExpression.Parse(options.Value.Maintenance.ScheduleCron);
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
            if (next is null) return;

            var delay = next.Value - DateTimeOffset.UtcNow;
            if (delay > MaximumTimerDelay)
            {
                await Task.Delay(MaximumTimerDelay, stoppingToken);
                continue;
            }

            await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, stoppingToken);
            maintenance.Start(MaintenanceRunTriggers.Scheduled, options.Value.Maintenance.DefaultScenarioId);
        }
    }
}
