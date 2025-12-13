using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace heygent.Core.Notification;

/// <summary>
/// Lark Bot API 클라이언트
/// 특정 사용자에게 DM 메시지를 보낼 수 있습니다.
/// </summary>
public class LarkBotClient
{
    private readonly ILogger<LarkBotClient> _logger;
    private const string BaseUrl = "https://open.larksuite.com/open-apis";
    private readonly string _appId;
    private readonly string _appSecret;
    private readonly HttpClient _httpClient;
    
    // 토큰 캐싱
    private string? _tenantAccessToken;
    private DateTime _tokenExpireTime = DateTime.MinValue;

    public LarkBotClient(ILogger<LarkBotClient> logger, string appId, string appSecret)
    {
        _logger = logger;
        _appId = appId;
        _appSecret = appSecret;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// tenant_access_token을 발급받습니다. (2시간 유효, 자동 캐싱)
    /// </summary>
    public async Task<string> GetTenantAccessTokenAsync()
    {
        // 캐시된 토큰이 유효하면 재사용
        if (!string.IsNullOrEmpty(_tenantAccessToken) && DateTime.UtcNow < _tokenExpireTime)
        {
            return _tenantAccessToken;
        }

        var requestBody = new LarkBotTokenRequest
        {
            AppId = _appId,
            AppSecret = _appSecret
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody, LarkBotJsonContext.Default.LarkBotTokenRequest),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync($"{BaseUrl}/auth/v3/tenant_access_token/internal", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get tenant access token: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<LarkBotTokenResponse>(responseBody, LarkBotJsonContext.Default.LarkBotTokenResponse);
        
        if (result?.Code != 0)
        {
            throw new Exception($"Failed to get tenant access token: {result?.Msg}");
        }

        _tenantAccessToken = result.TenantAccessToken;
        // 만료 5분 전에 갱신하도록 설정
        _tokenExpireTime = DateTime.UtcNow.AddSeconds(result.Expire - 300);

        return _tenantAccessToken!;
    }

    /// <summary>
    /// 이메일 주소로 텍스트 메시지를 발송합니다.
    /// </summary>
    /// <param name="email">수신자 이메일 주소</param>
    /// <param name="text">메시지 내용</param>
    public async Task<LarkBotSendMessageResponse> SendTextMessageAsync(string email, string text)
    {
        var token = await GetTenantAccessTokenAsync();

        var textContent = new LarkBotTextContent { Text = text };
        var requestBody = new LarkBotSendMessageRequest
        {
            ReceiveId = email,
            MsgType = "text",
            Content = JsonSerializer.Serialize(textContent, LarkBotJsonContext.Default.LarkBotTextContent)
        };

        return await SendMessageInternalAsync(token, requestBody);
    }

    /// <summary>
    /// 이메일 주소로 인터랙티브 카드 메시지를 발송합니다.
    /// </summary>
    /// <param name="email">수신자 이메일 주소</param>
    /// <param name="title">카드 제목</param>
    /// <param name="body">카드 본문 (마크다운 지원)</param>
    /// <param name="template">카드 색상 템플릿 (blue, green, yellow, orange, red, purple, grey)</param>
    public async Task<LarkBotSendMessageResponse> SendInteractiveCardAsync(string email, string title, string body, string template = "blue")
    {
        var token = await GetTenantAccessTokenAsync();

        var cardContent = new LarkBotCardContent
        {
            Config = new LarkBotCardConfig { WideScreenMode = true },
            Header = new LarkBotCardHeader
            {
                Title = new LarkBotCardTitle { Tag = "plain_text", Content = title },
                Template = template
            },
            Elements = new LarkBotCardElement[]
            {
                new LarkBotCardElement
                {
                    Tag = "div",
                    Text = new LarkBotCardText { Tag = "lark_md", Content = body }
                }
            }
        };

        var requestBody = new LarkBotSendMessageRequest
        {
            ReceiveId = email,
            MsgType = "interactive",
            Content = JsonSerializer.Serialize(cardContent, LarkBotJsonContext.Default.LarkBotCardContent)
        };

        return await SendMessageInternalAsync(token, requestBody);
    }

    /// <summary>
    /// 이메일 주소로 인터랙티브 카드 메시지를 발송합니다. (커스텀 카드 JSON)
    /// </summary>
    /// <param name="email">수신자 이메일 주소</param>
    /// <param name="cardJson">카드 JSON 문자열</param>
    public async Task<LarkBotSendMessageResponse> SendInteractiveCardRawAsync(string email, string cardJson)
    {
        var token = await GetTenantAccessTokenAsync();

        var requestBody = new LarkBotSendMessageRequest
        {
            ReceiveId = email,
            MsgType = "interactive",
            Content = cardJson
        };

        return await SendMessageInternalAsync(token, requestBody);
    }

    private async Task<LarkBotSendMessageResponse> SendMessageInternalAsync(string token, LarkBotSendMessageRequest requestBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/im/v1/messages?receive_id_type=email");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, LarkBotJsonContext.Default.LarkBotSendMessageRequest),
            Encoding.UTF8,
            "application/json"
        );

        _logger.LogInformation($"LarkBotClient - request.RequestUri={request.RequestUri}, token={token}");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to send message: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<LarkBotSendMessageResponse>(responseBody, LarkBotJsonContext.Default.LarkBotSendMessageResponse);

        if (result?.Code != 0)
        {
            throw new Exception($"Failed to send message: Code={result?.Code}, Msg={result?.Msg}");
        }

        return result;
    }
}

#region JSON Serialization Context (AOT 지원)

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(LarkBotTokenRequest))]
[JsonSerializable(typeof(LarkBotTokenResponse))]
[JsonSerializable(typeof(LarkBotSendMessageRequest))]
[JsonSerializable(typeof(LarkBotSendMessageResponse))]
[JsonSerializable(typeof(LarkBotTextContent))]
[JsonSerializable(typeof(LarkBotCardContent))]
[JsonSerializable(typeof(LarkBotCardConfig))]
[JsonSerializable(typeof(LarkBotCardHeader))]
[JsonSerializable(typeof(LarkBotCardTitle))]
[JsonSerializable(typeof(LarkBotCardElement))]
[JsonSerializable(typeof(LarkBotCardText))]
internal partial class LarkBotJsonContext : JsonSerializerContext
{
}

#endregion

#region DTO Classes

/// <summary>
/// tenant_access_token 요청 DTO
/// </summary>
public class LarkBotTokenRequest
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}

/// <summary>
/// tenant_access_token 응답 DTO
/// </summary>
public class LarkBotTokenResponse
{
    public LarkBotTokenResponse() { }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("tenant_access_token")]
    public string? TenantAccessToken { get; set; }

    [JsonPropertyName("expire")]
    public int Expire { get; set; } // 토큰 유효 시간 (초)
}

/// <summary>
/// 메시지 발송 요청 DTO
/// </summary>
public class LarkBotSendMessageRequest
{
    public string ReceiveId { get; set; } = string.Empty;
    public string MsgType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 텍스트 메시지 콘텐츠 DTO
/// </summary>
public class LarkBotTextContent
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 인터랙티브 카드 콘텐츠 DTO
/// </summary>
public class LarkBotCardContent
{
    public LarkBotCardConfig Config { get; set; } = new();
    public LarkBotCardHeader Header { get; set; } = new();
    public LarkBotCardElement[] Elements { get; set; } = Array.Empty<LarkBotCardElement>();
}

public class LarkBotCardConfig
{
    public bool WideScreenMode { get; set; } = true;
}

public class LarkBotCardHeader
{
    public LarkBotCardTitle Title { get; set; } = new();
    public string Template { get; set; } = "blue";
}

public class LarkBotCardTitle
{
    public string Tag { get; set; } = "plain_text";
    public string Content { get; set; } = string.Empty;
}

public class LarkBotCardElement
{
    public string Tag { get; set; } = "div";
    public LarkBotCardText Text { get; set; } = new();
}

public class LarkBotCardText
{
    public string Tag { get; set; } = "lark_md";
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 메시지 발송 응답 DTO
/// </summary>
public class LarkBotSendMessageResponse
{
    public LarkBotSendMessageResponse() { }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public LarkBotMessageData? Data { get; set; }
}

/// <summary>
/// 메시지 발송 응답 데이터
/// </summary>
public class LarkBotMessageData
{
    public LarkBotMessageData() { }

    [JsonPropertyName("message_id")]
    public string? MessageId { get; set; }

    [JsonPropertyName("root_id")]
    public string? RootId { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("msg_type")]
    public string? MsgType { get; set; }

    [JsonPropertyName("create_time")]
    public string? CreateTime { get; set; }

    [JsonPropertyName("update_time")]
    public string? UpdateTime { get; set; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }

    [JsonPropertyName("chat_id")]
    public string? ChatId { get; set; }
}

#endregion