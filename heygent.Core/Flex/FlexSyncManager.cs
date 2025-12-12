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
            // 모든 테이블에 대해서 CREATE (CREATE TABLE IF NOT EXISTS)
            await _repository.EnsureTablesAsync();

            // 인증 (authentication)
            // 액세스 토큰 갱신 (Refresh access token) - https://developers.flex.team/reference/authentication-token
            await _client.AuthenticateAsync();

            // 조직 (departments)
            // 조직 목록 조회 (Get all departments) - https://developers.flex.team/reference/departments-get-all-departments
            await _client.FetchAndSaveDepartmentsAsync();

            // 조직 조직장 (department-heads)
            // 조직 조직장 조회 (Get all department heads) - https://developers.flex.team/reference/getdepartmentsheads

            // 직무·직위·직책 (job-items)
            // 직무·직위·직책 목록 조회 (Get all job items) - https://developers.flex.team/reference/job-items-get-all-job-items

            // 구성원 (users)
            // 사번 목록 조회 (Get user employee numbers) - https://developers.flex.team/reference/users-get-employee-numbers

            // 구성원 마스터 (user-masters)
            // 사번으로 구성원 마스터 목록 조회 (Get user masters by employee numbers) - https://developers.flex.team/reference/user-masters-get-user-masters-by-employee-numbers

            // 구성원 조직 (user-departments)
            // 사번으로 구성원 조직·직책 목록 조회 (Get user department and job title by employee numbers) - https://developers.flex.team/reference/user-departments-get-user-departments-by-employee-numbers

            // 구성원 휴직 (user-leave-of-absence)
            // 사번으로 구성원 휴직 정보 조회 (Get user leave-of-absence by employee numbers) - https://developers.flex.team/reference/getuserleaveofabsencesbyemployeenumbers

            // 구성원 변경사항 (user-changes)
            // 날짜별 변동된 구성원 목록 조회 (Get user changes by date) - https://developers.flex.team/reference/user-changes-get-user-changes-by-date

            // 구성원 근무 스케줄 (user-work-schedules)
            // 사번으로 날짜별 구성원 근무 스케줄 조회 (Get user work schedules by date and employee numbers) - https://developers.flex.team/reference/user-work-schedules-get-user-work-schedules-by-date-and-employee-numbers
            // 사번으로 기간별 구성원 근무 스케줄 조회 (Get user work schedules by period and employee numbers) - https://developers.flex.team/reference/user-work-schedules-get-user-work-schedules-by-period-and-employee-numbers

            // 구성원 휴가 사용 (user-time-off-uses)
            // 사번으로 날짜별 휴가 사용 조회 (Get user time-off users by date and employee numbers) - https://developers.flex.team/reference/user-time-off-uses-get-user-time-off-uses-by-date-and-employee-numbers
            // 사번으로 기간별 휴가 사용 조회 (Get user time-off users by period and employee numbers) - https://developers.flex.team/reference/user-time-off-uses-get-user-time-off-uses-by-period-and-employee-numbers

            // 구성원 휴가 부여 (user-time-off-buckets)
            // 사번으로 구성원 연차 부여 목록 조회 (Get user annual time-off users by employee numbers) - https://developers.flex.team/reference/user-time-off-buckets-get-user-annual-time-off-buckets-by-employee-numbers

            // 구성원 가족 (user-family)
            // 사번으로 구성원 가족정보 조회 (Get user family details) - https://developers.flex.team/reference/getuserfamilybyemployeenumbers

            _logger.LogInformation("Flex data sync completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during Flex data sync.");
            throw; // 상위 스케줄러에서 에러 처리
        }
    }
}