using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Procura.API.AI.Gemini
{
    public class GeminiClient : IGeminiClient
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;
        private readonly ILogger<GeminiClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<GeminiClientResult> GenerateContentAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.Model))
            {
                var error = "Gemini model name is not configured. Please set 'Gemini:Model' in configuration.";
                _logger.LogError("{Error}", error);
                return GeminiClientResult.Fail(error);
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                var error = "Gemini API key is not configured. Please set 'Gemini:ApiKey' in configuration or User Secrets.";
                _logger.LogError("{Error}", error);
                return GeminiClientResult.Fail(error, isAuth: true);
            }

            var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

            var requestBody = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = $"{systemPrompt}\n\n=== USER INPUT DATA ===\n{userPrompt}" }
                        }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    ResponseMimeType = "application/json",
                    Temperature = 0.1
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);
            var maxRetries = Math.Max(0, _options.MaxRetries);
            int attempt = 0;

            while (true)
            {
                attempt++;
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

                    using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                    {
                        Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                    };

                    using var response = await _httpClient.SendAsync(request, cts.Token);
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseContent, JsonOptions);
                        var candidate = apiResponse?.Candidates is { Count: > 0 } ? apiResponse.Candidates[0] : null;
                        var part = candidate?.Content?.Parts is { Count: > 0 } ? candidate.Content.Parts[0] : null;

                        if (string.IsNullOrWhiteSpace(part?.Text))
                        {
                            return GeminiClientResult.Fail("Gemini returned an empty candidate response.");
                        }

                        return GeminiClientResult.Ok(part.Text);
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Gemini API rate limit exceeded (HTTP 429).");
                        return GeminiClientResult.Fail("Gemini API rate limit or quota exceeded. Please try again later.", isQuota: true);
                    }

                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogError("Gemini API authorization failure (HTTP {StatusCode}).", (int)response.StatusCode);
                        return GeminiClientResult.Fail("Gemini API authorization failure. Check your configured API key.", isAuth: true);
                    }

                    if ((int)response.StatusCode >= 500 && attempt <= maxRetries)
                    {
                        _logger.LogWarning("Gemini API server error (HTTP {StatusCode}). Retrying attempt {Attempt} of {MaxRetries}...", (int)response.StatusCode, attempt, maxRetries);
                        await Task.Delay(500 * attempt, ct);
                        continue;
                    }

                    _logger.LogError("Gemini API call failed with HTTP status {StatusCode}. Response: {Response}", (int)response.StatusCode, responseContent);
                    return GeminiClientResult.Fail($"Gemini API error (HTTP {(int)response.StatusCode}): {response.ReasonPhrase}");
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogError("Gemini request timed out after {Timeout}s.", _options.TimeoutSeconds);
                    return GeminiClientResult.Fail($"Gemini request timed out after {_options.TimeoutSeconds} seconds.");
                }
                catch (HttpRequestException ex) when (attempt <= maxRetries)
                {
                    _logger.LogWarning(ex, "Gemini network exception on attempt {Attempt} of {MaxRetries}. Retrying...", attempt, maxRetries);
                    await Task.Delay(500 * attempt, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error calling Gemini API.");
                    return GeminiClientResult.Fail($"Unexpected error calling Gemini API: {ex.Message}");
                }
            }
        }
    }
}
