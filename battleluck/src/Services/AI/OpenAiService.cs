using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BattleLuck.Models;
using BattleLuck.Models.Api;

namespace BattleLuck.Core
{
    /// <summary>Minimal server-side OpenAI Chat Completions provider for BattleLuck text requests.</summary>
    public sealed class OpenAiService : BaseAiService
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public override bool IsEnabled => !_disposed && !_authFailed && HasUsableConfiguration(_apiKey, _baseUrl, _model);
        public string Model => _model;
        public string BaseUrl => _baseUrl;

        public OpenAiService(string apiKey, string baseUrl, string model, int maxRequestsPerSecond = 2, int timeoutSeconds = 30)
            : base(maxRequestsPerSecond, timeoutSeconds)
        {
            _apiKey = apiKey?.Trim() ?? string.Empty;
            _baseUrl = (baseUrl?.Trim().TrimEnd('/') ?? string.Empty);
            _model = model?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_apiKey))
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public static bool HasUsableConfiguration(string? apiKey, string? baseUrl, string? model)
        {
            return IsUsableCredential(apiKey, minLength: 20) &&
                   Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
                   !string.IsNullOrWhiteSpace(model);
        }

        public async Task<string?> GetChatCompletionAsync(List<ChatMessage> messages, float temperature = 0.7f, int maxTokens = 300)
        {
            if (!IsEnabled)
                return null;

            await ApplyRateLimitAsync();
            try
            {
                var body = new ChatCompletionRequest
                {
                    Model = _model,
                    Temperature = temperature,
                    MaxCompletionTokens = Math.Max(1, maxTokens),
                    Messages = messages.ConvertAll(message => new ChatCompletionMessage
                    {
                        Role = NormalizeRole(message.Role),
                        Content = message.Content
                    })
                };
                var json = JsonSerializer.Serialize(body);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
                var responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        HandleAuthFailure("OpenAI", $"HTTP {(int)response.StatusCode}");
                    else
                        HandleHttpError("OpenAI", response.StatusCode, TrimError(responseJson));
                    return null;
                }

                using var document = JsonDocument.Parse(responseJson);
                var text = document.RootElement.TryGetProperty("choices", out var choices) &&
                           choices.GetArrayLength() > 0 &&
                           choices[0].TryGetProperty("message", out var message) &&
                           message.TryGetProperty("content", out var responseContent) &&
                           responseContent.ValueKind == JsonValueKind.String
                    ? responseContent.GetString()
                    : null;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    RecordSuccess();
                    return text;
                }

                LastError = "OpenAI response did not contain a text choice.";
                BattleLuckLogger.Warning(LastError);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                HandleTimeout("OpenAI", ex);
                return null;
            }
            catch (Exception ex)
            {
                HandleException("OpenAI", ex);
                return null;
            }
            finally
            {
                ReleaseRateLimit();
            }
        }

        private static string NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() switch
        {
            "system" => "system",
            "assistant" or "model" => "assistant",
            _ => "user"
        };

        private static string TrimError(string value) => value.Length <= 300 ? value : value[..300] + "...";
    }
}
