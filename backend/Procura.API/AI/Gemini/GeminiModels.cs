using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Procura.API.AI.Gemini
{
    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    public class GeminiContent
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class GeminiGenerationConfig
    {
        [JsonPropertyName("response_mime_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ResponseMimeType { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.1;
    }

    public class GeminiApiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("error")]
        public GeminiApiError? Error { get; set; }
    }

    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    public class GeminiApiError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class GeminiClientResult
    {
        public bool Success { get; set; }
        public string? Text { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsQuotaExceeded { get; set; }
        public bool IsAuthError { get; set; }

        public static GeminiClientResult Ok(string text) =>
            new GeminiClientResult { Success = true, Text = text };

        public static GeminiClientResult Fail(string error, bool isQuota = false, bool isAuth = false) =>
            new GeminiClientResult { Success = false, ErrorMessage = error, IsQuotaExceeded = isQuota, IsAuthError = isAuth };
    }
}
