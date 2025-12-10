using Microsoft.Extensions.Logging;

namespace heygent.Core.Flex;

public class FlexSyncManager
{
    private readonly ILogger<FlexSyncManager> _logger;
    private readonly FlexApiClient _client;
    private readonly FlexRepository _repository;

    public FlexSyncManager(ILogger<FlexSyncManager> logger, FlexApiClient client, FlexRepository repository)
    {
        _logger = logger;
        _client = client;
        _repository = repository;
    }

    public async Task SyncAllAsync()
    {
        _logger.LogInformation("Starting Flex data sync...");

        try 
        {
            // 테이블 존재 여부 확인 및 생성 (IF NOT EXISTS)
            await _repository.EnsureTablesAsync();

            // 1. 조직 동기화
            _logger.LogInformation("Syncing Organizations...");
            var orgs = await _client.GetOrganizationsAsync();
            await _repository.SaveOrganizationsAsync(orgs);
            _logger.LogInformation($"Synced {orgs.Count} Organizations.");

            // 2. 사원 동기화
            _logger.LogInformation("Syncing Employees...");
            var employees = await _client.GetEmployeesAsync();
            await _repository.SaveEmployeesAsync(employees);
            _logger.LogInformation($"Synced {employees.Count} Employees.");
            
            // 3. 휴가/휴직 동기화
            _logger.LogInformation("Syncing Leaves...");
            var leaves = await _client.GetLeavesAsync();
            await _repository.SaveLeavesAsync(leaves);
            _logger.LogInformation($"Synced {leaves.Count} Leaves.");

            // 4. 근무 스케줄 동기화
            _logger.LogInformation("Syncing Work Schedules...");
            var schedules = await _client.GetWorkSchedulesAsync();
            await _repository.SaveWorkSchedulesAsync(schedules);
            _logger.LogInformation($"Synced {schedules.Count} Work Schedules.");

            _logger.LogInformation("Flex data sync completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during Flex data sync.");
            throw; // 상위 스케줄러에서 에러 처리
        }
    }
}

