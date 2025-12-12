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

        // simple DDL based on ANSI SQL
        var sql = @"
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

            CREATE TABLE IF NOT EXISTS hr.flex_department (
                id SERIAL PRIMARY KEY,
                department_code VARCHAR(30) NOT NULL,
                name VARCHAR(300) NOT NULL,
                parent_department_code VARCHAR(30),
                display_order INT NOT NULL,
                visible BOOLEAN NOT NULL,
                begin_date DATE,
                end_date DATE,
                sort_order TEXT,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_department_head (
                id SERIAL PRIMARY KEY,
                department_code VARCHAR(30) NOT NULL,
                user_id VARCHAR(50) NOT NULL,
                is_direct BOOLEAN NOT NULL DEFAULT false,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_jobRoleCode (
                jobRoleCode VARCHAR(50) PRIMARY KEY,
                name VARCHAR(200),
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_jobRankCode (
                jobRankCode VARCHAR(50) PRIMARY KEY,
                name VARCHAR(200),
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS hr.flex_jobTitleCode (
                jobTitleCode VARCHAR(50) PRIMARY KEY,
                name VARCHAR(200),
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        ";

        await conn.ExecuteAsync(sql);
        _logger.LogInformation("Flex tables ensured.");
    }

    public async Task<int> InsertApiLogRequestAsync(string url, string method, string requestBody)
    {
        using var conn = CreateConnection();
        conn.Open();
        
        // AOT Compatibility: Use NpgsqlCommand directly instead of Dapper
        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var cmd = new NpgsqlCommand(@"INSERT INTO hr.flex_api_log (url, method, request_body)
                                              VALUES (@url, @method, @requestBody)
                                              RETURNING id", npgsqlConn);
            cmd.Parameters.AddWithValue("url", url ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("method", method ?? (object)DBNull.Value);
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

    public async Task SaveDepartmentsAsync(List<FlexDepartmentDto> items)
    {
        if (items == null || !items.Any()) return;

        using var conn = CreateConnection();
        conn.Open();

        // AOT Compatibility: Use NpgsqlCommand directly instead of Dapper
        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var trans = npgsqlConn.BeginTransaction();

            try
            {
                foreach (var item in items)
                {
                    // 1. Check existence
                    bool exists;
                    using (var checkCmd = new NpgsqlCommand("SELECT COUNT(1) FROM hr.flex_department WHERE department_code = @departmentCode", npgsqlConn, trans))
                    {
                        checkCmd.Parameters.AddWithValue("departmentCode", item.departmentCode);
                        var count = await checkCmd.ExecuteScalarAsync();
                        exists = Convert.ToInt32(count) > 0;
                    }

                    if (exists)
                    {
                        // 2. Update
                        using var updateCmd = new NpgsqlCommand(
                            @"UPDATE hr.flex_department 
                              SET name = @name, parent_department_code = @parentDepartmentCode, 
                                  display_order = @displayOrder, visible = @visible, 
                                  begin_date = @beginDate, end_date = @endDate, sort_order = @sortOrder, 
                                  updated_at = CURRENT_TIMESTAMP
                              WHERE department_code = @departmentCode", npgsqlConn, trans);
                        
                        updateCmd.Parameters.AddWithValue("departmentCode", item.departmentCode);
                        updateCmd.Parameters.AddWithValue("name", item.name);
                        updateCmd.Parameters.AddWithValue("parentDepartmentCode", item.parentDepartmentCode ?? (object)DBNull.Value);
                        updateCmd.Parameters.AddWithValue("displayOrder", item.displayOrder);
                        updateCmd.Parameters.AddWithValue("visible", item.visible);
                        updateCmd.Parameters.AddWithValue("beginDate", item.beginDate.HasValue ? (object)item.beginDate.Value : DBNull.Value);
                        updateCmd.Parameters.AddWithValue("endDate", item.endDate.HasValue ? (object)item.endDate.Value : DBNull.Value);
                        updateCmd.Parameters.AddWithValue("sortOrder", item.sortOrder ?? (object)DBNull.Value);

                        await updateCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // 3. Insert
                        using var insertCmd = new NpgsqlCommand(
                            @"INSERT INTO hr.flex_department (department_code, name, parent_department_code, display_order, visible, begin_date, end_date, sort_order, updated_at)
                              VALUES (@departmentCode, @name, @parentDepartmentCode, @displayOrder, @visible, @beginDate, @endDate, @sortOrder, CURRENT_TIMESTAMP)", npgsqlConn, trans);
                              
                        insertCmd.Parameters.AddWithValue("departmentCode", item.departmentCode);
                        insertCmd.Parameters.AddWithValue("name", item.name);
                        insertCmd.Parameters.AddWithValue("parentDepartmentCode", item.parentDepartmentCode ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("displayOrder", item.displayOrder);
                        insertCmd.Parameters.AddWithValue("visible", item.visible);
                        insertCmd.Parameters.AddWithValue("beginDate", item.beginDate.HasValue ? (object)item.beginDate.Value : DBNull.Value);
                        insertCmd.Parameters.AddWithValue("endDate", item.endDate.HasValue ? (object)item.endDate.Value : DBNull.Value);
                        insertCmd.Parameters.AddWithValue("sortOrder", item.sortOrder ?? (object)DBNull.Value);

                        await insertCmd.ExecuteNonQueryAsync();
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
        else
        {
            throw new NotSupportedException("Only Npgsql is supported for AOT compatibility in this method.");
        }
    }

    public async Task<List<string>> GetAllDepartmentCodesAsync()
    {
        using var conn = CreateConnection();
        conn.Open();

        var result = new List<string>();

        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var cmd = new NpgsqlCommand("SELECT department_code FROM hr.flex_department", npgsqlConn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    result.Add(reader.GetString(0));
                }
            }
        }
        else
        {
            throw new NotSupportedException("Only Npgsql is supported for AOT compatibility in this method.");
        }

        return result;
    }

    public async Task SaveDepartmentHeadsAsync(List<FlexDepartmentHeadResponseDto> items)
    {
        if (items == null || !items.Any()) return;

        using var conn = CreateConnection();
        conn.Open();

        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var trans = npgsqlConn.BeginTransaction();
            try
            {
                foreach (var item in items)
                {
                    // 1. Delete existing heads for this department
                    using (var deleteCmd = new NpgsqlCommand("DELETE FROM hr.flex_department_head WHERE department_code = @departmentCode", npgsqlConn, trans))
                    {
                        deleteCmd.Parameters.AddWithValue("departmentCode", item.departmentCode);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    // 2. Insert Direct Heads
                    if (item.directHeadUsers != null)
                    {
                        foreach (var head in item.directHeadUsers)
                        {
                            using var insertCmd = new NpgsqlCommand(
                                @"INSERT INTO hr.flex_department_head (department_code, user_id, is_direct, updated_at)
                                  VALUES (@departmentCode, @userId, true, CURRENT_TIMESTAMP)", npgsqlConn, trans);
                            
                            insertCmd.Parameters.AddWithValue("departmentCode", item.departmentCode);
                            insertCmd.Parameters.AddWithValue("userId", head.employeeNumber);
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 3. Insert Resolved Heads (indirect)
                    if (item.resolvedHeadUsers != null)
                    {
                        foreach (var head in item.resolvedHeadUsers)
                        {
                             // Exclude if already inserted as direct
                             bool isDirect = item.directHeadUsers?.Any(d => d.employeeNumber == head.employeeNumber) ?? false;
                             if (!isDirect)
                             {
                                using var insertCmd = new NpgsqlCommand(
                                    @"INSERT INTO hr.flex_department_head (department_code, user_id, is_direct, updated_at)
                                      VALUES (@departmentCode, @userId, false, CURRENT_TIMESTAMP)", npgsqlConn, trans);
                                
                                insertCmd.Parameters.AddWithValue("departmentCode", item.departmentCode);
                                insertCmd.Parameters.AddWithValue("userId", head.employeeNumber);
                                await insertCmd.ExecuteNonQueryAsync();
                             }
                        }
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
        else
        {
            throw new NotSupportedException("Only Npgsql is supported for AOT compatibility in this method.");
        }
    }

    public async Task SaveJobItemsAsync(FlexJobItemsResponseDto data)
    {
        using var conn = CreateConnection();
        conn.Open();

        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var trans = npgsqlConn.BeginTransaction();
            try
            {
                // 1. Job Roles
                if (data.jobRoles != null)
                {
                    foreach (var item in data.jobRoles)
                    {
                        using var cmd = new NpgsqlCommand(
                            @"INSERT INTO hr.flex_jobRoleCode (jobRoleCode, name, updated_at)
                              VALUES (@code, @name, CURRENT_TIMESTAMP)
                              ON CONFLICT (jobRoleCode) 
                              DO UPDATE SET name = EXCLUDED.name, updated_at = CURRENT_TIMESTAMP", npgsqlConn, trans);
                        cmd.Parameters.AddWithValue("code", item.jobRoleCode);
                        cmd.Parameters.AddWithValue("name", item.name);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // 2. Job Ranks
                if (data.jobRanks != null)
                {
                    foreach (var item in data.jobRanks)
                    {
                        using var cmd = new NpgsqlCommand(
                            @"INSERT INTO hr.flex_jobRankCode (jobRankCode, name, updated_at)
                              VALUES (@code, @name, CURRENT_TIMESTAMP)
                              ON CONFLICT (jobRankCode) 
                              DO UPDATE SET name = EXCLUDED.name, updated_at = CURRENT_TIMESTAMP", npgsqlConn, trans);
                        cmd.Parameters.AddWithValue("code", item.jobRankCode);
                        cmd.Parameters.AddWithValue("name", item.name);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // 3. Job Titles
                if (data.jobTitles != null)
                {
                    foreach (var item in data.jobTitles)
                    {
                        using var cmd = new NpgsqlCommand(
                            @"INSERT INTO hr.flex_jobTitleCode (jobTitleCode, name, updated_at)
                              VALUES (@code, @name, CURRENT_TIMESTAMP)
                              ON CONFLICT (jobTitleCode) 
                              DO UPDATE SET name = EXCLUDED.name, updated_at = CURRENT_TIMESTAMP", npgsqlConn, trans);
                        cmd.Parameters.AddWithValue("code", item.jobTitleCode);
                        cmd.Parameters.AddWithValue("name", item.name);
                        await cmd.ExecuteNonQueryAsync();
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
        else
        {
            throw new NotSupportedException("Only Npgsql is supported for AOT compatibility in this method.");
        }
    }
}