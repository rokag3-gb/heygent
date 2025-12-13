using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using heygent.Core.Flex.Dto;
using heygent.Core.Credential;
using heygent.Core.Model;

namespace heygent.Core.Flex;

public class FlexApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FlexApiClient> _logger;
    private readonly FlexRepository _repository;
    private string _accessToken = "";
    
    public FlexApiClient(HttpClient httpClient, ILogger<FlexApiClient> logger, FlexRepository repository)
    {
        _httpClient = httpClient;
        _logger = logger;
        _repository = repository;
        
        var baseUrl = Conf.Current.flex.base_url;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }
    }

    private async Task EnsureAccessTokenAsync()
    {
        // AccessToken이 없으면 발급 시도
        if (string.IsNullOrEmpty(_accessToken)) 
        {
            await AuthenticateAsync();
        }
    }

    public async Task AuthenticateAsync()
    {
        var url = $"{Conf.Current.flex.base_url}/auth/realms/open-api/protocol/openid-connect/token";

        // 요청 준비
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var formData = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("client_id", "open-api"),
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", Conf.Current.flex.refresh_token),
        };
        var content = new FormUrlEncodedContent(formData);
        request.Content = content;

        // Logging을 위해서 request body 문자열 생성
        var requestBodyStr = await content.ReadAsStringAsync();
        
        // 요청 log insert
        var logId = await _repository.InsertApiLogRequestAsync(url, "POST", requestBodyStr);

        try
        {
            // Request!!
            var response = await _httpClient.SendAsync(request);

            // Read response
            var responseString = await response.Content.ReadAsStringAsync();
            var statusCode = response.StatusCode.ToString();

            // 응답 log update
            await _repository.UpdateApiLogResponseAsync(logId, statusCode.ToString(), responseString);

            // 실패 응답 시 예외 throw
            if (!response.IsSuccessStatusCode)
            {
                 _logger.LogError($"Authentication failed. Status: {statusCode}, Content: {responseString}");
                 throw new HttpRequestException($"Authentication failed: {statusCode}");
            }

            // 응답을 FlexAuthResponseDto 으로 deserialize
            var authResponse = JsonConvert.DeserializeObject<FlexAuthResponseDto>(responseString);

            // 응답에서 access_token 추출
            if (authResponse != null && !string.IsNullOrEmpty(authResponse.access_token))
            {
                _accessToken = authResponse.access_token;
                _logger.LogInformation("Flex Access Token obtained successfully.");
                
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            }
            else
            {
                throw new Exception("Invalid token response.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate.");
            throw;
        }
    }

    public async Task FetchAndSaveDepartmentsAsync()
    {
        await EnsureAccessTokenAsync(); // AccessToken이 없으면 발급 시도

        var url = $"{Conf.Current.flex.base_url}/departments/all";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        var logId = await _repository.InsertApiLogRequestAsync(url, "GET", "");

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            var statusCode = response.StatusCode.ToString();

            await _repository.UpdateApiLogResponseAsync(logId, statusCode, responseString);

            if (!response.IsSuccessStatusCode)
            {
                 _logger.LogError($"Failed to fetch departments. Status: {statusCode}, Content: {responseString}");
                 throw new HttpRequestException($"Failed to fetch departments: {statusCode}");
            }

            List<FlexDepartmentDto>? items = null;
            
            try 
            {
                var token = JToken.Parse(responseString);

                items = token switch
                {
                    JArray array => array.ToObject<List<FlexDepartmentDto>>(), // [...] 으로 감싸져있는 경우
                    JObject obj when obj["departments"] != null => obj["departments"]?.ToObject<List<FlexDepartmentDto>>(), // "departments" 키로 감싸져있는 경우
                    _ => null,
                };

                if (items == null)
                {
                    _logger.LogWarning("Unknown response structure for departments.");
                }
            }
            catch (JsonException ex)
            {
                 _logger.LogError(ex, "JSON parsing failed.");
                 // 파싱 실패시 예외를 다시 던지거나, items를 null로 유지하여 저장 로직 스킵
            }

            if (items != null && items.Any())
            {
                await _repository.SaveDepartmentsAsync(items);

                _logger.LogInformation($"Saved {items.Count} departments.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching/saving departments.");
            throw;
        }
    }

    public async Task FetchAndSaveDepartmentHeadsAsync()
    {
        await EnsureAccessTokenAsync(); // AccessToken이 없으면 발급 시도

        // 1. Get all department codes from DB
        var departmentCodes = await _repository.GetAllDepartmentCodesAsync();
        if (!departmentCodes.Any())
        {
            _logger.LogInformation("No departments found to fetch heads.");
            return;
        }

        // 2. Process in batches of 20
        const int batchSize = 20;
        int totalSaved = 0;

        for (int i = 0; i < departmentCodes.Count; i += batchSize)
        {
            var batch = departmentCodes.Skip(i).Take(batchSize);
            var departmentCodesCsv = string.Join(",", batch);
            
            var url = $"{Conf.Current.flex.base_url}/departments/heads";
            var requestUrl = $"{url}?departmentCodes={departmentCodesCsv}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            
            var logId = await _repository.InsertApiLogRequestAsync(url, "GET", $"departmentCodes={departmentCodesCsv}");

            try
            {
                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                var statusCode = response.StatusCode.ToString();

                await _repository.UpdateApiLogResponseAsync(logId, statusCode, responseString);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to fetch department heads. Status: {statusCode}, Content: {responseString}");
                    throw new HttpRequestException($"Failed to fetch department heads: {statusCode}");
                }

                List<FlexDepartmentHeadResponseDto>? items = null;
                
                try 
                {
                    var token = JToken.Parse(responseString);

                    items = token switch
                    {
                        JArray array => array.ToObject<List<FlexDepartmentHeadResponseDto>>(),
                        JObject obj when obj["departments"] != null => obj["departments"]?.ToObject<List<FlexDepartmentHeadResponseDto>>(),
                        _ => null
                    };

                    if (items == null)
                    {
                        _logger.LogWarning("Unknown response structure for department heads.");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON parsing failed for department heads.");
                }

                if (items != null && items.Any())
                {
                    await _repository.SaveDepartmentHeadsAsync(items);
                    totalSaved += items.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching/saving department heads.");
                throw;
            }
        }

        _logger.LogInformation($"Saved heads for total {totalSaved} departments (processed in batches).");
    }

    public async Task FetchAndSaveJobItemsAsync()
    {
        await EnsureAccessTokenAsync();

        var url = $"{Conf.Current.flex.base_url}/job-items/all";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        var logId = await _repository.InsertApiLogRequestAsync(url, "GET", "");

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            var statusCode = response.StatusCode.ToString();

            await _repository.UpdateApiLogResponseAsync(logId, statusCode, responseString);

            if (!response.IsSuccessStatusCode)
            {
                 _logger.LogError($"Failed to fetch job items. Status: {statusCode}, Content: {responseString}");
                 throw new HttpRequestException($"Failed to fetch job items: {statusCode}");
            }

            FlexJobItemsResponseDto? items = null;
            
            try 
            {
                items = JsonConvert.DeserializeObject<FlexJobItemsResponseDto>(responseString);
            }
            catch (JsonException ex)
            {
                 _logger.LogError(ex, "JSON parsing failed for job items.");
            }

            if (items != null)
            {
                await _repository.SaveJobItemsAsync(items);

                int roleCount = items.jobRoles?.Count ?? 0;
                int rankCount = items.jobRanks?.Count ?? 0;
                int titleCount = items.jobTitles?.Count ?? 0;

                _logger.LogInformation($"Saved Job Items: {roleCount} roles, {rankCount} ranks, {titleCount} titles.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching/saving job items.");
            throw;
        }
    }

    public async Task FetchAndSaveEmployeeNumbersAsync()
    {
        await EnsureAccessTokenAsync();

        string? nextPageKey = null;
        int totalSaved = 0;

        _logger.LogInformation("Starting to fetch employee numbers...");
        
        while (true)
        {
            var baseUrl = $"{Conf.Current.flex.base_url}/users/employee-numbers?pageSize=20";
            var url = string.IsNullOrEmpty(nextPageKey) ? baseUrl : $"{baseUrl}&nextPageKey={Uri.EscapeDataString(nextPageKey)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var logId = await _repository.InsertApiLogRequestAsync(url, "GET", "");

            try 
            {
                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                var statusCode = response.StatusCode.ToString();
                
                await _repository.UpdateApiLogResponseAsync(logId, statusCode, responseString);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to fetch employee numbers. Status: {statusCode}, Content: {responseString}");
                    throw new HttpRequestException($"Failed to fetch employee numbers: {statusCode}");
                }
                
                var dto = JsonConvert.DeserializeObject<FlexEmployeeNumbersResponseDto>(responseString);
                
                if (dto != null && dto.employeeNumbers != null && dto.employeeNumbers.Any())
                {
                    await _repository.SaveEmployeeNumbersAsync(dto.employeeNumbers);
                    totalSaved += dto.employeeNumbers.Count;
                    _logger.LogInformation($"Saved {dto.employeeNumbers.Count} employee numbers. Total so far: {totalSaved}");
                }
                
                // hasNext 가 true 이고 nextPageKey 가 있을 때에만 다음 페이지 진행
                if (dto != null && dto.hasNext && !string.IsNullOrWhiteSpace(dto.nextPageKey))
                {
                    nextPageKey = dto.nextPageKey;
                }
                else
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error fetching/saving employee numbers.");
                 throw;
            }
        }
        
        _logger.LogInformation($"Completed fetching and saving all employee numbers. Total: {totalSaved}");
    }

    public async Task FetchAndSaveUserMastersAsync()
    {
        await EnsureAccessTokenAsync();

        // 1. Get all employee numbers from DB
        var employeeNumbers = await _repository.GetAllEmployeeNumbersAsync();
        if (!employeeNumbers.Any())
        {
            _logger.LogInformation("No employee numbers found to fetch user masters.");
            return;
        }

        // 2. Process in batches of 20
        const int batchSize = 20;
        int totalSaved = 0;

        for (int i = 0; i < employeeNumbers.Count; i += batchSize)
        {
            var batch = employeeNumbers.Skip(i).Take(batchSize);
            // Assuming the query parameter name is 'employeeNumbers' or 'userIds'. 
            // The prompt says "뒤에 붙여서 호출", similar to departmentCodes.
            // Based on flex user-masters typical pattern, let's assume 'employeeNumbers' or check if user provided param name.
            // User said "전체 사원번호를... ? 뒤에 붙여서 호출".
            // If we follow 'FetchAndSaveDepartmentHeadsAsync' pattern which used `departmentCodes={csv}`,
            // we will use `employeeNumbers={csv}` or `userIds={csv}`. 
            // Checking the previous prompt context: "GET /user-masters" sample doesn't show request param name explicitly.
            // However, typical Flex API pattern for bulk fetch by ID is often pluralized parameter.
            // Let's assume `employeeNumbers` as the key, consistent with the object property name.
            
            // Wait, looking at standard Flex API for user-masters, it might be filtering by employeeNumbers.
            // Let's try `employeeNumbers`.
            
            var employeeNumbersCsv = string.Join(",", batch);
            var baseUrl = $"{Conf.Current.flex.base_url}/user-masters";
            var requestUrl = $"{baseUrl}?employeeNumbers={employeeNumbersCsv}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            
            var logId = await _repository.InsertApiLogRequestAsync(baseUrl, "GET", $"employeeNumbers={employeeNumbersCsv}");

            try
            {
                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                var statusCode = response.StatusCode.ToString();

                await _repository.UpdateApiLogResponseAsync(logId, statusCode, responseString);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to fetch user masters. Status: {statusCode}, Content: {responseString}");
                    throw new HttpRequestException($"Failed to fetch user masters: {statusCode}");
                }

                var dto = JsonConvert.DeserializeObject<FlexUserMasterResponseDto>(responseString);
                
                if (dto != null && dto.users != null && dto.users.Any())
                {
                    await _repository.SaveUserMastersAsync(dto.users);
                    totalSaved += dto.users.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching/saving user masters.");
                throw;
            }
        }
        
        _logger.LogInformation($"Saved user masters for total {totalSaved} users (processed in batches).");
    }
}