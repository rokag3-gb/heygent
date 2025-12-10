using Cronos;
using heygent.Core;
using heygent.Core.Flex;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace heygent.Scheduler;

public class FlexSyncService : BackgroundService
{
    private readonly ILogger<FlexSyncService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<CronExpression> _cronExpressions = new();
    private readonly TimeZoneInfo _timeZone;
    private readonly string _instanceId = Guid.NewGuid().ToString().Substring(36 - 12, 12); // instanceId = uuid는 length 36으로 너무 길기 때문에 가독성을 높이기 위해 의도적으로 우측 끝 12자리로 한정
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // 동시성을 제어하기 위한 SemaphoreSlim 추가

    public FlexSyncService(ILogger<FlexSyncService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        // Cronos 5 field expression (분 시 일 월 요일)
        // 매 5분마다
        //_cronExpressions.Add(CronExpression.Parse("*/5 * * * *"));

        // Cronos - 6 field expression (초 분 시 일 월 요일)
        // "*/10 * * * * *" -> 매 10초마다
        // "0 0 1 * * *" -> 매일 KST 10시마다 (UTC 1시)
        // "0 0 21,23 * * *" -> 매일 KST 6시, 8시마다 (UTC 21시, 23시)
        // "0 30 20-23 * * *" -> 매일 KST 5시~8시 사이 매 정각 30분마다 (UTC 20시~23시)
        //_cronExpressions.Add(CronExpression.Parse("*/5 * * * * *", CronFormat.IncludeSeconds));

        // Conf.Current.schedule.cron_expression_flex_sync 파싱
        Conf.Current.schedule.cron_expression_flex_sync.ForEach(expr =>
        {
            if (CronExpression.TryParse(expr, CronFormat.IncludeSeconds, out var parsedCron))
                _cronExpressions.Add(parsedCron);
            else
                _logger.LogWarning($"Invalid cron expression (flex_sync): {expr}.");
        });

        //_timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); // KST (Korea Standard Time)
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(Conf.Current.schedule.time_zone) ?? TimeZoneInfo.Local;

        // TimeZoneInfo.FindSystemTimeZoneById(Conf.Current.schedule.time_zone)
        // _timeZone.Id = "Asia/Seoul"
        // _timeZone.DisplayName = "(UTC+09:00) 서울"

        // TimeZoneInfo.Local
        // _timeZone.Id = "Korea Standard Time"
        // _timeZone.DisplayName = "(UTC+09:00) 서울"

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
                // 바로 실행하면 loop가 과열될 수 있기 때문에 의도적으로 짧은 backoff delay (0.2초) 설정
                await Task.Delay(200, stoppingToken);

                var nextUtc = nextExecutionTimes.First();
                var delay = nextUtc - DateTimeOffset.UtcNow;

                if (delay.TotalMilliseconds > 0)
                    await Task.Delay(delay, stoppingToken);

                // 세마포어를 즉시 확보할 수 있는지 확인한다.
                // 즉시 확보할 수 없으면 false를 반환한다.
                if (!await _semaphore.WaitAsync(0, stoppingToken))
                {
                    _logger.LogWarning("Skipped: Previous run still active running.");
                    continue; // 락을 얻지 못하면 이번 실행은 건너뜁니다.
                }

                try
                {
                    _logger.LogInformation($"FlexSync Active Running: InstanceId={_instanceId}, TID={Environment.CurrentManagedThreadId}");
                    
                    // Create Scope & Resolve Service
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var syncManager = scope.ServiceProvider.GetRequiredService<FlexSyncManager>();
                        await syncManager.SyncAllAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the Flex sync job.");
                }
                finally
                {
                    // 작업이 성공하든 실패하든 반드시 세마포어를 해제.
                    _semaphore.Release();
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