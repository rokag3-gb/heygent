using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
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
        var url = $"{Conf.Current.flex.base_url}auth/realms/open-api/protocol/openid-connect/token";

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

    private async Task<T> GetAsync<T>(string uri)
    {
        await EnsureAccessTokenAsync();

        var response = await _httpClient.GetAsync(uri);
        
        // 401 Unauthorized 발생 시 토큰 갱신 후 1회 재시도
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Unauthorized access. Retrying after authentication...");
            await AuthenticateAsync();
            response = await _httpClient.GetAsync(uri);
        }

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(content) ?? throw new Exception("Failed to deserialize response.");
    }

    public async Task<List<FlexOrganizationDto>> GetOrganizationsAsync()
    {
        // URL 예시
        return await GetAsync<List<FlexOrganizationDto>>("v2/organizations");
    }

    public async Task<List<FlexEmployeeDto>> GetEmployeesAsync()
    {
        return await GetAsync<List<FlexEmployeeDto>>("v2/users"); 
    }

    public async Task<List<FlexLeaveDto>> GetLeavesAsync()
    {
        return await GetAsync<List<FlexLeaveDto>>("v2/time-off/applications");
    }

    public async Task<List<FlexWorkScheduleDto>> GetWorkSchedulesAsync()
    {
        return await GetAsync<List<FlexWorkScheduleDto>>("v2/work-schedules");
    }
}