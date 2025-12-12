using System.Net.Http.Json;
using heygent.Core.Credential;
using heygent.Core.Gemini.Dto;

namespace heygent.Core.Gemini;

public class GeminiApiClient
{
    private readonly HttpClient _httpClient;

    /*
    # Google Gemini 모델 설명 (2025년 12월 기준)
    gemini-3-pro (또는 gemini-3-pro-preview): 가장 최신 기술이 집약된 모델로 복잡한 추론, 코딩, 창의적 작업에 적합
    gemini-2.5-flash: 매우 빠르고 저렴하며 성능도 준수합니다. 챗봇이나 단순 요약에 가장 많이 쓰입니다.
    gemini-2.5-pro: 3.0이 나오기 전까지 가장 강력했던 모델로, 안정적인 고성능이 필요할 때 좋습니다.
    gemini-2.0-flash-thinking-exp (Thinking 모델): 특수 목적. 답변하기 전에 "생각(Thinking)" 과정을 거쳐 더 논리적인 답변을 내놓는 실험적 모델입니다.

    # 버전을 명시하지 않고 최신 모델 별칭 (Alias) 사용하기
    gemini-pro: 현재 시점의 안정적인 Pro 버전으로 자동 연결
    gemini-flash: 현재 시점의 안정적인 Flash 버전으로 자동 연결
    */
    private const string DefaultModel = "gemini-2.5-flash"; // "gemini-3-pro-preview"; //"gemini-1.5-flash"; 
    /*
    # API version
    /v1beta : Gemini 3와 같은 최신 모델이나 실험적인 기능(Thinking Mode 등)은 보통 Beta 버전에 먼저 공개됩니다. 최신 기능을 가장 먼저 써보려면 이쪽이 유리합니다.
    /v1 : Stable. 기업용 서비스 등 안정성이 최우선일 때 사용합니다. 아주 최신 모델은 아직 v1에 반영되지 않았을 수도 있습니다.
    */
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 사용 가능한 Gemini 모델 목록을 조회합니다.
    /// </summary>
    /// <returns>모델 정보 목록</returns>
    public async Task<List<GeminiModelInfo>> ListModelsAsync()
    {
        var apiKey = GeminiSecret.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API Key가 설정되지 않았습니다.");
        }

        var requestUrl = $"{BaseUrl}?key={apiKey}"; // List models endpoint: GET /v1beta/models

        try
        {
            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Gemini 모델 목록 조회 실패: {response.StatusCode}, 상세: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync(GeminiJsonContext.Default.GeminiModelListResponse);
            return result?.Models ?? new List<GeminiModelInfo>();
        }
        catch (Exception ex)
        {
            throw new Exception($"Gemini 모델 목록 조회 중 오류 발생: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gemini API를 호출하여 텍스트 응답을 생성합니다.
    /// </summary>
    /// <param name="prompt">입력 프롬프트</param>
    /// <param name="model">사용할 모델 (기본값: gemini-1.5-flash)</param>
    /// <returns>생성된 텍스트</returns>
    public async Task<string> GenerateContentAsync(string prompt, string model = DefaultModel)
    {
        var apiKey = GeminiSecret.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API Key가 설정되지 않았습니다. heygent.Core/Credential/Secret.cs 파일을 확인해주세요.");
        }

        var requestUrl = $"{BaseUrl}/{model}:generateContent?key={apiKey}";

        var requestBody = new GeminiRequest
        {
            Contents = new List<GeminiContent>
            {
                new GeminiContent
                {
                    Parts = new List<GeminiPart>
                    {
                        new GeminiPart { Text = prompt }
                    }
                }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(requestUrl, requestBody, GeminiJsonContext.Default.GeminiRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Gemini API 호출 실패: {response.StatusCode}, 상세: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync(GeminiJsonContext.Default.GeminiResponse);

            if (result?.Candidates != null && result.Candidates.Count > 0)
            {
                var text = result.Candidates[0].Content?.Parts?.FirstOrDefault()?.Text;
                return text ?? string.Empty;
            }
            
            return string.Empty;
        }
        catch (Exception ex)
        {
            // 실제 운영 환경에서는 로깅이 필요합니다.
            // 여기서는 예외를 다시 던지거나 적절히 처리합니다.
            throw new Exception($"Gemini API 호출 중 오류 발생: {ex.Message}", ex);
        }
    }
}