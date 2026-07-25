using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace BattleLuck.Services.AI;

public class LlamaAIService : BaseAiService
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _maxRequestsPerSecond;
    private readonly int _timeoutSeconds;
    private readonly HttpClient _http = new();

    public LlamaAIService(string apiKey, string baseUrl, string model, int maxRequestsPerSecond, int timeoutSeconds)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl;
        _model = model;
        _maxRequestsPerSecond = maxRequestsPerSecond;
        _timeoutSeconds = timeoutSeconds;
    }

    public static bool HasUsableConfiguration(string apiKey, string baseUrl)
    {
        return !string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(baseUrl);
    }

    public override async Task<string?> QueryAsync(string systemPrompt, string userMessage, bool allowFallback = true)
    {
        // Local Ollama implementation
        try
        {
            var payload = new
            {
                model = _model,
                prompt = userMessage,
                stream = false,
                options = new { temperature = 0.7f }
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var response = await _http.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/api/generate")
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                },
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Llama API returned {response.StatusCode}";
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("response", out var resp))
            {
                LastError = null;
                return resp.GetString();
            }
            return null;
        }
        catch (HttpRequestException ex)
        {
            LastError = $"Llama connection failed: {ex.Message}";
            return null;
        }
        catch (OperationCanceledException)
        {
            LastError = "Llama request timeout";
            return null;
        }
    }
}