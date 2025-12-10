using Cronos;
using heygent.Core;
using heygent.Core.Notification;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace heygent.Scheduler;

public class AgentNotificationService : BackgroundService
{
    private readonly ILogger<AgentNotificationService> _logger;
    private readonly NotificationService _notifier;
    private readonly List<CronExpression> _cronExpressions = new();
    private readonly TimeZoneInfo _timeZone;
    private readonly string _instanceId = Guid.NewGuid().ToString().Substring(36 - 12, 12);

    public AgentNotificationService(ILogger<AgentNotificationService> logger, NotificationService notifier)
    {
        _logger = logger;
        _notifier = notifier;

        Conf.Current.schedule.cron_expression_notification.ForEach(expr =>
        {
            if (CronExpression.TryParse(expr, CronFormat.IncludeSeconds, out var parsedCron))
                _cronExpressions.Add(parsedCron);
            else
                _logger.LogWarning($"Invalid cron expression (notification): {expr}");
        });

        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(Conf.Current.schedule.time_zone) ?? TimeZoneInfo.Local;
        
        var cronExpressionCsv = string.Join(", ", _cronExpressions.Select(expr => $"[{expr}]"));
        _logger.LogInformation($"AgentNotificationService initialized. Cron: {cronExpressionCsv}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentNotificationService started");

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

                try
                {
                    _logger.LogInformation($"Agent Notification Service Job Active Running: InstanceId={_instanceId}, TID={Environment.CurrentManagedThreadId}");
                    
                    // TODO: 실제 알림 발송 로직 구현
                    // 1. 대상 직원 조회 (DB)
                    // 2. 메시지 생성
                    // 3. 발송 (NotificationService 사용)
                    
                    // _logger.LogInformation("Agent notification job logic placeholder.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the Agent Notification job.");
                }
            }
            else
            {
                // 실행할 스케줄이 없으면 긴 대기
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        _logger.LogInformation("AgentNotificationService stopped.");
    }
}