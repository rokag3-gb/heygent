using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using heygent.Core.Flex.Dto;
using heygent.Core.Model;

namespace heygent.Core.Flex;

public class FlexApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FlexApiClient> _logger;
    private string _accessToken = "";
    
    public FlexApiClient(HttpClient httpClient, ILogger<FlexApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
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
            await RefreshTokenAsync();
        }
    }

    private async Task RefreshTokenAsync()
    {
        var refreshToken = Conf.Current.flex.refresh_token;
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new Exception("Flex Refresh Token is empty.");
        }

        // Flex API 스펙에 맞게 조정 필요 (예시: POST /oauth/token)
        var requestBody = new
        {
            grant_type = "refresh_token",
            refresh_token = refreshToken
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        
        try 
        {
            // 실제 인증 URL은 문서 확인 필요. 여기서는 예시로 작성.
            // 사용자가 "flex API 에서 특정 URL 로 refresh_token 을 POST 하면 access_token 을 주거든" 이라고 함.
            var response = await _httpClient.PostAsync("oauth/token", content); 
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Token refresh failed. Status: {response.StatusCode}, Content: {errorContent}");
                throw new HttpRequestException($"Token refresh failed: {response.StatusCode}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var authResponse = JsonConvert.DeserializeObject<FlexAuthResponseDto>(responseString);

            if (authResponse != null && !string.IsNullOrEmpty(authResponse.access_token))
            {
                _accessToken = authResponse.access_token;
                _logger.LogInformation("Flex Access Token refreshed.");

                // 갱신된 refresh_token이 있다면 업데이트 (Optional)
                if (!string.IsNullOrEmpty(authResponse.refresh_token))
                {
                    Conf.Current.flex.refresh_token = authResponse.refresh_token;
                }
                
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            }
            else
            {
                throw new Exception("Invalid token response.");
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to refresh access token.");
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
            _logger.LogWarning("Unauthorized access. Retrying after token refresh...");
            await RefreshTokenAsync();
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

