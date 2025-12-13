using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace heygent.Core.Notification;

/// <summary>
/// Slack Bot API 클라이언트
/// 채널 메시지 전송 및 사용자 DM 전송 기능을 제공합니다.
/// </summary>
public class SlackBotClient
{
    private readonly ILogger<SlackBotClient> _logger;
    private const string BaseUrl = "https://slack.com/api";
    private readonly string _botToken;
    private readonly HttpClient _httpClient;

    public SlackBotClient(ILogger<SlackBotClient> logger, string botToken)
    {
        _logger = logger;
        _botToken = botToken;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _botToken);
    }

    /// <summary>
    /// 특정 채널(또는 사용자 ID)로 텍스트 메시지를 발송합니다.
    /// </summary>
    /// <param name="channelId">채널 ID (C...) 또는 사용자 ID (U...)</param>
    /// <param name="text">발송할 메시지 내용</param>
    public async Task<SlackSendMessageResponse> SendMessageAsync(string channelId, string text)
    {
        var requestBody = new SlackSendMessageRequest
        {
            Channel = channelId,
            Text = text
        };

        return await SendMessageInternalAsync(requestBody);
    }

    /// <summary>
    /// 제목과 본문, 색상 띠가 포함된 카드 형태의 메시지를 발송합니다.
    /// </summary>
    /// <param name="channelId">채널 ID 또는 유저 ID</param>
    /// <param name="title">제목</param>
    /// <param name="body">본문 (Markdown 지원)</param>
    /// <param name="color">측면 색상 띠 (예: #36a64f)</param>
    public async Task<SlackSendMessageResponse> SendCardMessageAsync(string channelId, string title, string body, string color = "#36a64f")
    {
        var blocks = new List<SlackBlock>
        {
            new SlackBlock
            {
                Type = "header",
                Text = new SlackTextObject { Type = "plain_text", Text = title, Emoji = true }
            },
            new SlackBlock
            {
                Type = "section",
                Text = new SlackTextObject { Type = "mrkdwn", Text = body }
            }
        };

        var attachment = new SlackAttachment
        {
            Color = color,
            Blocks = blocks
        };

        var requestBody = new SlackSendMessageRequest
        {
            Channel = channelId,
            Text = title, // 모바일 알림 등에 표시될 텍스트
            Attachments = new List<SlackAttachment> { attachment }
        };

        return await SendMessageInternalAsync(requestBody);
    }

    /// <summary>
    /// 이메일 주소를 통해 사용자를 찾고 DM을 발송합니다.
    /// </summary>
    /// <param name="email">사용자 이메일</param>
    /// <param name="text">메시지 내용</param>
    public async Task<SlackSendMessageResponse> SendDirectMessageAsync(string email, string text)
    {
        var userId = await GetUserIdByEmailAsync(email);
        if (string.IsNullOrEmpty(userId))
        {
            throw new Exception($"Could not find Slack user with email: {email}");
        }

        return await SendMessageAsync(userId, text);
    }

    /// <summary>
    /// 이메일 주소를 통해 사용자를 찾고 카드 형태의 DM을 발송합니다.
    /// </summary>
    public async Task<SlackSendMessageResponse> SendDirectCardMessageAsync(string email, string title, string body, string color = "#36a64f")
    {
        var userId = await GetUserIdByEmailAsync(email);
        if (string.IsNullOrEmpty(userId))
        {
            throw new Exception($"Could not find Slack user with email: {email}");
        }

        return await SendCardMessageAsync(userId, title, body, color);
    }

    /// <summary>
    /// 커스텀 Attachment 목록을 사용하여 메시지를 발송합니다.
    /// </summary>
    public async Task<SlackSendMessageResponse> SendCustomMessageAsync(string channelId, string text, List<SlackAttachment> attachments)
    {
        var requestBody = new SlackSendMessageRequest
        {
            Channel = channelId,
            Text = text,
            Attachments = attachments
        };

        return await SendMessageInternalAsync(requestBody);
    }

    /// <summary>
    /// 이메일로 Slack User ID를 조회합니다.
    /// </summary>
    public async Task<string?> GetUserIdByEmailAsync(string email)
    {
        var requestUri = $"{BaseUrl}/users.lookupByEmail?email={Uri.EscapeDataString(email)}";
        
        var response = await _httpClient.GetAsync(requestUri);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to lookup user by email. Status: {StatusCode}, Body: {Body}", response.StatusCode, responseBody);
            return null;
        }

        var result = JsonSerializer.Deserialize<SlackUserLookupResponse>(responseBody, SlackBotJsonContext.Default.SlackUserLookupResponse);

        if (result == null || !result.Ok)
        {
            _logger.LogWarning("Slack user lookup failed for email {Email}: {Error}", email, result?.Error);
            return null;
        }

        return result.User?.Id;
    }

    private async Task<SlackSendMessageResponse> SendMessageInternalAsync(SlackSendMessageRequest requestBody)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody, SlackBotJsonContext.Default.SlackSendMessageRequest),
            Encoding.UTF8,
            "application/json"
        );

        _logger.LogInformation("Sending Slack message to {Channel}", requestBody.Channel);

        var response = await _httpClient.PostAsync($"{BaseUrl}/chat.postMessage", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to send Slack message: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<SlackSendMessageResponse>(responseBody, SlackBotJsonContext.Default.SlackSendMessageResponse);

        if (result == null || !result.Ok)
        {
            throw new Exception($"Slack API Error: {result?.Error ?? "Unknown error"}");
        }

        return result;
    }
}

#region JSON Serialization Context

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(SlackSendMessageRequest))]
[JsonSerializable(typeof(SlackSendMessageResponse))]
[JsonSerializable(typeof(SlackUserLookupResponse))]
[JsonSerializable(typeof(SlackUser))]
[JsonSerializable(typeof(SlackAttachment))]
[JsonSerializable(typeof(SlackBlock))]
[JsonSerializable(typeof(SlackTextObject))]
[JsonSerializable(typeof(SlackButtonElement))]
[JsonSerializable(typeof(List<object>))]
internal partial class SlackBotJsonContext : JsonSerializerContext
{
}

#endregion

#region DTO Classes

public class SlackSendMessageRequest
{
    public string Channel { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SlackAttachment>? Attachments { get; set; }
}

public class SlackAttachment
{
    public string? Color { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SlackBlock>? Blocks { get; set; }
}

public class SlackBlock
{
    public string Type { get; set; } = string.Empty; // header, section, divider, context, actions

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SlackTextObject? Text { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SlackTextObject>? Fields { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? Elements { get; set; } // Context (TextObject) or Actions (ButtonElement)
}

public class SlackButtonElement
{
    public string Type { get; set; } = "button";
    public SlackTextObject Text { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Style { get; set; } // primary, danger

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }
}

public class SlackTextObject
{
    public string Type { get; set; } = string.Empty; // plain_text, mrkdwn
    public string Text { get; set; } = string.Empty;
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Emoji { get; set; }
}

public class SlackSendMessageResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? Ts { get; set; } // Timestamp
}

public class SlackUserLookupResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public SlackUser? User { get; set; }
}

public class SlackUser
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RealName { get; set; } = string.Empty;
}

#endregion