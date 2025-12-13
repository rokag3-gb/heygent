using Dapper;
using heygent.Core.Flex.Dto;
using heygent.Core.Model;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using Newtonsoft.Json;

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

            CREATE TABLE IF NOT EXISTS hr.flex_employee (
                employee_number VARCHAR(100) PRIMARY KEY,
                name VARCHAR(200),
                email VARCHAR(200),
                name_in_office VARCHAR(200),
                english_name_first VARCHAR(100),
                english_name_last VARCHAR(100),
                mobile_phone VARCHAR(50),
                ssn VARCHAR(50),
                birthday DATE,
                gender VARCHAR(20),
                profile_image_url TEXT,
                company_group_join_date DATE,
                company_join_date DATE,
                company_leave_date DATE,
                employment_contract VARCHAR(50),
                home_address_country VARCHAR(100),
                home_address_state VARCHAR(100),
                home_address_city VARCHAR(100),
                home_address_1 VARCHAR(300),
                home_address_2 VARCHAR(300),
                home_address_3 VARCHAR(300),
                home_address_zip_code VARCHAR(20),
                primary_department_code VARCHAR(50),
                primary_department_name VARCHAR(200),
                primary_job_role_code VARCHAR(50),
                primary_job_rank_code VARCHAR(50),
                primary_job_title_code VARCHAR(50),
                custom_properties TEXT,
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

    public async Task<List<string>> GetAllEmployeeNumbersAsync()
    {
        using var conn = CreateConnection();
        conn.Open();

        var result = new List<string>();

        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var cmd = new NpgsqlCommand("SELECT employee_number FROM hr.flex_employee", npgsqlConn);
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

    public async Task SaveEmployeeNumbersAsync(List<string> employeeNumbers)
    {
        if (employeeNumbers == null || !employeeNumbers.Any()) return;

        using var conn = CreateConnection();
        conn.Open();

        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var trans = npgsqlConn.BeginTransaction();
            try
            {
                foreach (var empNo in employeeNumbers)
                {
                    using var cmd = new NpgsqlCommand(
                        @"INSERT INTO hr.flex_employee (employee_number, updated_at)
                          VALUES (@empNo, CURRENT_TIMESTAMP)
                          ON CONFLICT (employee_number) 
                          DO UPDATE SET updated_at = CURRENT_TIMESTAMP", npgsqlConn, trans);
                    cmd.Parameters.AddWithValue("empNo", empNo);
                    await cmd.ExecuteNonQueryAsync();
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

    public async Task SaveUserMastersAsync(List<FlexUserMasterDto> users)
    {
        if (users == null || !users.Any()) return;

        using var conn = CreateConnection();
        conn.Open();

        if (conn is NpgsqlConnection npgsqlConn)
        {
            using var trans = npgsqlConn.BeginTransaction();
            try
            {
                foreach (var user in users)
                {
                    var sql = @"
                        UPDATE hr.flex_employee
                        SET 
                            name = @name,
                            name_in_office = @nameInOffice,
                            english_name_first = @englishNameFirst,
                            english_name_last = @englishNameLast,
                            email = @email,
                            mobile_phone = @mobilePhone,
                            ssn = @ssn,
                            birthday = @birthday,
                            gender = @gender,
                            profile_image_url = @profileImageUrl,
                            company_group_join_date = @companyGroupJoinDate,
                            company_join_date = @companyJoinDate,
                            company_leave_date = @companyLeaveDate,
                            employment_contract = @employmentContract,
                            home_address_country = @homeAddressCountry,
                            home_address_state = @homeAddressState,
                            home_address_city = @homeAddressCity,
                            home_address_1 = @homeAddress1,
                            home_address_2 = @homeAddress2,
                            home_address_3 = @homeAddress3,
                            home_address_zip_code = @homeAddressZipCode,
                            primary_department_code = @primaryDepartmentCode,
                            primary_department_name = @primaryDepartmentName,
                            primary_job_role_code = @primaryJobRoleCode,
                            primary_job_rank_code = @primaryJobRankCode,
                            primary_job_title_code = @primaryJobTitleCode,
                            custom_properties = @customProperties,
                            updated_at = CURRENT_TIMESTAMP
                        WHERE employee_number = @employeeNumber
                    ";

                    using var cmd = new NpgsqlCommand(sql, npgsqlConn, trans);

                    // Helpers
                    object ToDbVal(string? s) => (object?)s ?? DBNull.Value;
                    object ToDbDate(string? s) => DateTime.TryParse(s, out var d) ? (object)d : DBNull.Value;

                    cmd.Parameters.AddWithValue("employeeNumber", user.employeeNumber);
                    cmd.Parameters.AddWithValue("name", ToDbVal(user.name));
                    cmd.Parameters.AddWithValue("nameInOffice", ToDbVal(user.nameInOffice));
                    cmd.Parameters.AddWithValue("englishNameFirst", ToDbVal(user.englishName?.firstName));
                    cmd.Parameters.AddWithValue("englishNameLast", ToDbVal(user.englishName?.lastName));
                    cmd.Parameters.AddWithValue("email", ToDbVal(user.email));
                    
                    var mobile = user.phoneNumbers?.FirstOrDefault(p => p.type == "PERSONAL")?.value;
                    cmd.Parameters.AddWithValue("mobilePhone", ToDbVal(mobile));
                    
                    cmd.Parameters.AddWithValue("ssn", ToDbVal(user.ssn));
                    cmd.Parameters.AddWithValue("birthday", ToDbDate(user.birthday));
                    cmd.Parameters.AddWithValue("gender", ToDbVal(user.gender));
                    cmd.Parameters.AddWithValue("profileImageUrl", ToDbVal(user.profileImageUrl));
                    cmd.Parameters.AddWithValue("companyGroupJoinDate", ToDbDate(user.companyGroupJoinDate));
                    cmd.Parameters.AddWithValue("companyJoinDate", ToDbDate(user.companyJoinDate));
                    cmd.Parameters.AddWithValue("companyLeaveDate", ToDbDate(user.companyLeaveDate));
                    cmd.Parameters.AddWithValue("employmentContract", ToDbVal(user.employmentContract));
                    
                    cmd.Parameters.AddWithValue("homeAddressCountry", ToDbVal(user.homeAddress?.addressCountry));
                    cmd.Parameters.AddWithValue("homeAddressState", ToDbVal(user.homeAddress?.addressState));
                    cmd.Parameters.AddWithValue("homeAddressCity", ToDbVal(user.homeAddress?.addressCity));
                    cmd.Parameters.AddWithValue("homeAddress1", ToDbVal(user.homeAddress?.address1));
                    cmd.Parameters.AddWithValue("homeAddress2", ToDbVal(user.homeAddress?.address2));
                    cmd.Parameters.AddWithValue("homeAddress3", ToDbVal(user.homeAddress?.address3));
                    cmd.Parameters.AddWithValue("homeAddressZipCode", ToDbVal(user.homeAddress?.addressZipCode));
                    
                    cmd.Parameters.AddWithValue("primaryDepartmentCode", ToDbVal(user.primaryDepartment?.departmentCode));
                    cmd.Parameters.AddWithValue("primaryDepartmentName", ToDbVal(user.primaryDepartment?.name));
                    cmd.Parameters.AddWithValue("primaryJobRoleCode", ToDbVal(user.primaryJobRole?.jobRoleCode));
                    cmd.Parameters.AddWithValue("primaryJobRankCode", ToDbVal(user.primaryJobRank?.jobRankCode));
                    cmd.Parameters.AddWithValue("primaryJobTitleCode", ToDbVal(user.primaryJobTitle?.jobTitleCode));
                    
                    var customPropsJson = user.customProperties != null ? JsonConvert.SerializeObject(user.customProperties) : null;
                    cmd.Parameters.AddWithValue("customProperties", ToDbVal(customPropsJson));

                    await cmd.ExecuteNonQueryAsync();
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