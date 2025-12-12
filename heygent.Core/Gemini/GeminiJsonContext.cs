using System.Text.Json.Serialization;
using heygent.Core.Gemini.Dto;

namespace heygent.Core.Gemini;

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GeminiRequest))]
[JsonSerializable(typeof(GeminiResponse))]
[JsonSerializable(typeof(GeminiModelListResponse))]
public partial class GeminiJsonContext : JsonSerializerContext
{
}