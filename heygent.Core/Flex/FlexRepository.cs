using Dapper;
using heygent.Core.Flex.Dto;
using heygent.Core.Model;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace heygent.Core.Flex;

public class FlexRepository
{
    private readonly ILogger<FlexRepository> _logger;
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;

    public FlexRepository(ILogger<FlexRepository> logger)
    {
        _logger = logger;
        // Conf.Current가 초기화된 이후에 호출된다고 가정
        _connectionString = Conf.Current.database.connection_string;
        _provider = Conf.Current.database.provider;
    }

    private IDbConnection CreateConnection()
    {
        if (_provider.ToString().Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        {
            return new NpgsqlConnection(_connectionString);
        }
        else
        {
            throw new NotSupportedException($"Provider '{_provider}' is not supported. Only 'postgresql' is supported currently.");
        }
    }

    public async Task EnsureTablesAsync()
    {
        using var conn = CreateConnection();
        conn.Open();

        // 아주 심플한 DDL (ANSI SQL 데이터 타입 사용 지향)
        // PostgreSQL: VARCHAR, TEXT, TIMESTAMP 등
        var sql = @"
            CREATE TABLE IF NOT EXISTS hr.flex_organization (
                code VARCHAR(100) PRIMARY KEY,
                name VARCHAR(200),
                parent_code VARCHAR(100),
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_employee (
                user_id VARCHAR(100) PRIMARY KEY,
                name VARCHAR(100),
                email VARCHAR(200),
                employee_number VARCHAR(100),
                organization_code VARCHAR(100),
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_leave (
                leave_id VARCHAR(100) PRIMARY KEY,
                user_id VARCHAR(100),
                type VARCHAR(100),
                start_date TIMESTAMP,
                end_date TIMESTAMP,
                status VARCHAR(50),
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_work_schedule (
                schedule_id VARCHAR(100) PRIMARY KEY,
                user_id VARCHAR(100),
                work_date TIMESTAMP, -- date 예약어 피함
                start_time VARCHAR(20),
                end_time VARCHAR(20),
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_api_log (
                id SERIAL PRIMARY KEY,
                request_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                url TEXT,
                method VARCHAR(10),
                status_code VARCHAR(10),
                request_header VARCHAR(255),
                request_body TEXT,
                response_body TEXT,
                response_at TIMESTAMP
            );
        ";

        await conn.ExecuteAsync(sql);
        _logger.LogInformation("Flex tables ensured.");
    }

    public async Task SaveOrganizationsAsync(List<FlexOrganizationDto> items)
    {
        if (items == null || !items.Any()) return;

        using var conn = CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            foreach (var item in items)
            {
                // 1. Check existence
                var exists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM hr.flex_organization WHERE code = @code", 
                    new { code = item.code }, trans) > 0;

                if (exists)
                {
                    // 2. Update
                    await conn.ExecuteAsync(
                        @"UPDATE hr.flex_organization 
                          SET name = @name, parent_code = @parentCode, updated_at = CURRENT_TIMESTAMP
                          WHERE code = @code", 
                        item, trans);
                }
                else
                {
                    // 3. Insert
                    await conn.ExecuteAsync(
                        @"INSERT INTO hr.flex_organization (code, name, parent_code, updated_at)
                          VALUES (@code, @name, @parentCode, CURRENT_TIMESTAMP)", 
                        item, trans);
                }
            }
            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task SaveEmployeesAsync(List<FlexEmployeeDto> items)
    {
        if (items == null || !items.Any()) return;

        using var conn = CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            foreach (var item in items)
            {
                var exists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM hr.flex_employee WHERE user_id = @userId", 
                    new { userId = item.userId }, trans) > 0;

                if (exists)
                {
                    await conn.ExecuteAsync(
                        @"UPDATE hr.flex_employee 
                          SET name = @name, email = @email, employee_number = @employeeNumber, organization_code = @organizationCode, updated_at = CURRENT_TIMESTAMP
                          WHERE user_id = @userId", 
                        item, trans);
                }
                else
                {
                    await conn.ExecuteAsync(
                        @"INSERT INTO hr.flex_employee (user_id, name, email, employee_number, organization_code, updated_at)
                          VALUES (@userId, @name, @email, @employeeNumber, @organizationCode, CURRENT_TIMESTAMP)", 
                        item, trans);
                }
            }
            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task SaveLeavesAsync(List<FlexLeaveDto> items)
    {
        if (items == null || !items.Any()) return;

        using var conn = CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            foreach (var item in items)
            {
                var exists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM hr.flex_leave WHERE leave_id = @leaveId", 
                    new { leaveId = item.leaveId }, trans) > 0;

                if (exists)
                {
                    await conn.ExecuteAsync(
                        @"UPDATE hr.flex_leave 
                          SET user_id = @userId, type = @type, start_date = @startDate, end_date = @endDate, status = @status, updated_at = CURRENT_TIMESTAMP
                          WHERE leave_id = @leaveId", 
                        item, trans);
                }
                else
                {
                    await conn.ExecuteAsync(
                        @"INSERT INTO hr.flex_leave (leave_id, user_id, type, start_date, end_date, status, updated_at)
                          VALUES (@leaveId, @userId, @type, @startDate, @endDate, @status, CURRENT_TIMESTAMP)", 
                        item, trans);
                }
            }
            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task SaveWorkSchedulesAsync(List<FlexWorkScheduleDto> items)
    {
        if (items == null || !items.Any()) return;

        using var conn = CreateConnection();
        conn.Open();
        using var trans = conn.BeginTransaction();

        try
        {
            foreach (var item in items)
            {
                var exists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM hr.flex_work_schedule WHERE schedule_id = @scheduleId", 
                    new { scheduleId = item.scheduleId }, trans) > 0;

                if (exists)
                {
                    await conn.ExecuteAsync(
                        @"UPDATE hr.flex_work_schedule 
                          SET user_id = @userId, work_date = @date, start_time = @startTime, end_time = @endTime, updated_at = CURRENT_TIMESTAMP
                          WHERE schedule_id = @scheduleId", 
                        item, trans);
                }
                else
                {
                    await conn.ExecuteAsync(
                        @"INSERT INTO hr.flex_work_schedule (schedule_id, user_id, work_date, start_time, end_time, updated_at)
                          VALUES (@scheduleId, @userId, @date, @startTime, @endTime, CURRENT_TIMESTAMP)", 
                        item, trans);
                }
            }
            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task<int> InsertApiLogRequestAsync(string url, string method, string requestHeader, string requestBody)
    {
        using var conn = CreateConnection();
        conn.Open();
        
        // AOT Compatibility: Use NpgsqlCommand directly instead of Dapper
        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var cmd = new NpgsqlCommand(@"INSERT INTO hr.flex_api_log (url, method, request_header, request_body)
                                              VALUES (@url, @method, @requestHeader, @requestBody)
                                              RETURNING id", npgsqlConn);
            cmd.Parameters.AddWithValue("url", url ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("method", method ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("requestHeader", requestHeader ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("requestBody", requestBody ?? (object)DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        else
        {
            throw new NotSupportedException("Only Npgsql is supported for AOT compatibility in this method.");
        }
    }

    public async Task UpdateApiLogResponseAsync(int id, string statusCode, string responseBody)
    {
        using var conn = CreateConnection();
        conn.Open();

        // AOT Compatibility: Use NpgsqlCommand directly instead of Dapper
        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var cmd = new NpgsqlCommand(@"UPDATE hr.flex_api_log
                                              SET status_code = @statusCode,
                                                  response_body = @responseBody,
                                                  response_at = CURRENT_TIMESTAMP
                                              WHERE id = @id", npgsqlConn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("statusCode", statusCode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("responseBody", responseBody ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            throw new NotSupportedException("Only Npgsql is supported for AOT compatibility in this method.");
        }
    }
}