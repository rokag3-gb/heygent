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
}