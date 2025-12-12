namespace heygent.Core.Gemini.Dto;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();

    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

public class GeminiModelListResponse
{
    [JsonPropertyName("models")]
    public List<GeminiModelInfo>? Models { get; set; }
}

public class GeminiModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("supportedGenerationMethods")]
    public List<string>? SupportedGenerationMethods { get; set; }
}
