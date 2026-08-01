using System.Net.Http;
using System.Text;
using System.Text.Json;
using BattleLuck.Core;
using ChatMessage = BattleLuck.Core.ChatMessage;

namespace BattleLuck.Services.AI;

public class LlamaAIService : BaseAiService
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public override bool IsEnabled => !_disposed && !_authFailed && HasUsableConfiguration(_apiKey, _baseUrl);
    public bool IsCrashed { get; private set; }
    public string Model => _model;
    public string ApiBaseUrl => _baseUrl;

    public LlamaAIService(string apiKey, string baseUrl, string model, int maxRequestsPerSecond, int timeoutSeconds)
        : base(maxRequestsPerSecond, timeoutSeconds)
    {
        _apiKey = apiKey ?? string.Empty;
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434" : baseUrl;
        _model = string.IsNullOrWhiteSpace(model) ? "llama3" : model;

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }

    public static bool HasUsableConfiguration(string? apiKey, string? baseUrl)
    {
        return !string.IsNullOrWhiteSpace(baseUrl) || BaseAiService.IsUsableCredential(apiKey, 8);
    }

    public async Task<string?> GetChatCompletionAsync(List<ChatMessage> messages, float temperature = 0.8f, int maxTokens = 300)
    {
        if (!IsEnabled)
            return null;

        await ApplyRateLimitAsync();
        try
        {
            // Build a single prompt from chat messages (system + rest)
            var systemPrompt = string.Join("\n\n", messages
                .Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c)));

            var dialog = string.Join("\n", messages
                .Where(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                .Select(m => $"{m.Role}: {m.Content}"));

            var finalPrompt = string.IsNullOrWhiteSpace(systemPrompt) ? dialog : systemPrompt + "\n\n" + dialog;

            var url = $"{_baseUrl.TrimEnd('/')}/api/generate";
            var payload = new
            {
                model = _model,
                prompt = finalPrompt,
                stream = false,
                options = new { temperature = temperature, num_predict = maxTokens }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                {
                    HandleAuthFailure("Llama", $"{response.StatusCode}: {responseJson}");
                    return null;
                }

                // Mark as crashed on common local runtime failures
                if ((int)response.StatusCode >= 500)
                    IsCrashed = true;

                HandleHttpError("Llama", response.StatusCode, responseJson);
                return null;
            }

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("response", out var resp))
            {
                RecordSuccess();
                IsCrashed = false;
                return resp.GetString();
            }

            LastError = "Missing 'response' in Llama API output";
            return null;
        }
        catch (TaskCanceledException ex)
        {
            HandleTimeout("Llama", ex);
            return null;
        }
        catch (HttpRequestException ex)
        {
            IsCrashed = true;
            HandleException("Llama", ex);
            return null;
        }
        catch (Exception ex)
        {
            HandleException("Llama", ex);
            return null;
        }
        finally
        {
            ReleaseRateLimit();
        }
    }
}