using Cronos;
using heygent.Core;
using heygent.Core.Flex;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace heygent.Scheduler;

public class FlexSyncService : BackgroundService
{
    private readonly ILogger<FlexSyncService> _logger;
    private readonly FlexSyncManager _syncManager;
    private readonly List<CronExpression> _cronExpressions = new();
    private readonly TimeZoneInfo _timeZone;
    private readonly string _instanceId = Guid.NewGuid().ToString().Substring(36 - 12, 12);

    public FlexSyncService(ILogger<FlexSyncService> logger, FlexSyncManager syncManager)
    {
        _logger = logger;
        _syncManager = syncManager;

        // Conf.Current.schedule.flex_sync 파싱
        Conf.Current.schedule.flex_sync.ForEach(expr =>
        {
            if (CronExpression.TryParse(expr, CronFormat.IncludeSeconds, out var parsedCron))
                _cronExpressions.Add(parsedCron);
            else
                _logger.LogWarning($"Invalid cron expression (flex_sync): {expr}.");
        });

        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(Conf.Current.schedule.time_zone) ?? TimeZoneInfo.Local;
        
        var cronExpressionCsv = string.Join(", ", _cronExpressions.Select(expr => $"[{expr}]"));
        _logger.LogInformation($"FlexSyncService initialized. Cron: {cronExpressionCsv}, TimeZone: {_timeZone.Id}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlexSyncService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextExecutionTimes = _cronExpressions
                .Select(expr => expr.GetNextOccurrence(DateTimeOffset.UtcNow, _timeZone, false))
                .Where(time => time.HasValue)
                .Select(time => time!.Value)
                .OrderBy(time => time)
                .ToList();

            if (nextExecutionTimes.Any())
            {
                // 약간의 딜레이
                await Task.Delay(200, stoppingToken);

                var nextUtc = nextExecutionTimes.First();
                var delay = nextUtc - DateTimeOffset.UtcNow;

                if (delay.TotalMilliseconds > 0)
                    await Task.Delay(delay, stoppingToken);

                try
                {
                    _logger.LogInformation($"FlexSync Active Running: InstanceId={_instanceId}, TID={Environment.CurrentManagedThreadId}");
                    
                    await _syncManager.SyncAllAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the Flex sync job.");
                }
            }
            else
            {
                // 실행할 스케줄이 없으면 긴 대기
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        _logger.LogInformation("FlexSyncService stopped.");
    }
}

