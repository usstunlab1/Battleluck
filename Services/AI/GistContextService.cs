using BattleLuck.Models;
using System.Text.Json;
using Unity.Mathematics;

namespace BattleLuck.Services.AI;

public class GistContextService
{
    private readonly AIConfig _config;
    private readonly HttpClient _http = new();
    private readonly ConcurrentDictionary<string, (string Content, DateTime Fetched)> _cache = new();
    private Timer? _syncTimer;
    
    public bool IsEnabled { get; private set; }
    public DateTime? LastSyncUtc { get; private set; }
    public string? LastError { get; private set; }
    public int CacheSize => _cache.Count;

    public GistContextService(AIConfig config)
    {
        _config = config;
        IsEnabled = config.GistSync.Enabled == true && 
                    !string.IsNullOrEmpty(config.GistSync.GistId) &&
                    !string.IsNullOrEmpty(config.GistSync.GitHubToken);
        
        if (IsEnabled && config.GistSync.AutoSync)
        {
            StartAutoSync();
        }
    }

    public async Task<string?> GetCatalogContextAsync(string filename, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return null;

        try
        {
            if (_cache.TryGetValue(filename, out var cached))
            {
                if (DateTime.UtcNow - cached.Fetched < TimeSpan.FromMinutes(5))
                    return cached.Content;
            }

            var content = await FetchFileFromGistAsync(filename, ct);
            if (content != null)
            {
                _cache[filename] = (content, DateTime.UtcNow);
                LastSyncUtc = DateTime.UtcNow;
            }
            return content;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to fetch {filename}: {ex.Message}";
            BattleLuckLogger.Warning($"[GistContext] {LastError}");
            return null;
        }
    }

    public async Task<Dictionary<string, string>> GetAllCatalogsAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        if (!IsEnabled) return result;

        var files = await ListGistFilesAsync(ct);
        foreach (var file in files)
        {
            var content = await GetCatalogContextAsync(file, ct);
            if (content != null)
                result[file] = content;
        }
        return result;
    }

    public async Task<bool> SyncFileAsync(string filename, string content, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, 
                $"https://api.github.com/gists/{_config.GistSync.GistId}");
            
            request.Headers.Add("Authorization", $"token {_config.GistSync.GitHubToken}");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            
            var body = new { files = new Dictionary<string, object> { [filename] = new { content } } };
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Gist sync failed: {response.StatusCode}";
                return false;
            }

            _cache[filename] = (content, DateTime.UtcNow);
            LastSyncUtc = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Sync error: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> SyncAllCatalogsAsync(CancellationToken ct = default)
    {
        if (!IsEnabled) return false;

        var success = true;
        foreach (var file in _config.GistSync.CatalogFiles)
        {
            var localPath = Path.Combine(ConfigLoader.ConfigRoot, file);
            if (!File.Exists(localPath))
                continue;

            var content = await File.ReadAllTextAsync(localPath, ct);
            if (!await SyncFileAsync(file, content, ct))
                success = false;
        }
        return success;
    }

    async Task<string?> FetchFileFromGistAsync(string filename, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/gists/{_config.GistSync.GistId}");
        
        request.Headers.Add("Authorization", $"token {_config.GistSync.GitHubToken}");
        request.Headers.Add("Accept", "application/vnd.github.v3+json");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var gist = JsonSerializer.Deserialize<GitHubGist>(await response.Content.ReadAsStringAsync(ct));
        if (gist?.Files.TryGetValue(filename, out var file) == true)
            return file.Content;

        return null;
    }

    async Task<List<string>> ListGistFilesAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/gists/{_config.GistSync.GistId}");
        
        request.Headers.Add("Authorization", $"token {_config.GistSync.GitHubToken}");
        request.Headers.Add("Accept", "application/vnd.github.v3+json");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return new List<string>();

        var gist = JsonSerializer.Deserialize<GitHubGist>(await response.Content.ReadAsStringAsync(ct));
        return gist?.Files.Keys.ToList() ?? new List<string>();
    }

    void StartAutoSync()
    {
        if (_config.GistSync.SyncIntervalSeconds <= 0)
            return;

        _syncTimer = new Timer(_ =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await SyncAllCatalogsAsync();
                }
                catch (Exception ex)
                {
                    BattleLuckLogger.Warning($"[GistContext] Auto-sync failed: {ex.Message}");
                }
            });
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _http?.Dispose();
    }

    private class GitHubGist
    {
        public Dictionary<string, GistFile> Files { get; set; } = new();
    }

    private class GistFile
    {
        public string? Content { get; set; }
        public int Size { get; set; }
        public string? Language { get; set; }
    }
}