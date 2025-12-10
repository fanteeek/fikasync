using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Spectre.Console;

namespace FikaSync;

public class GitHubClient
{
    private readonly HttpClient _client;
    
    public GitHubClient(string token)
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://api.github.com");
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FikaSync", "1.0"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<bool> TestToken()
    {
        try
        {
            var response = await _client.GetAsync("/user");
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var data = JsonNode.Parse(jsonString);
                string login = data?["login"]?.ToString() ?? "Unknown";
                Logger.Info($"[green]√[/] Authorized as: [bold]{login}[/]");
                return true;
            }
            else
            {
                Logger.Error($"GitHub error: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Network error: {ex.Message}");
            return false;
        }
    }

    public (string Owner, string Repo) ExtractRepoInfo(string url)
    {
        var cleanUrl = url.Trim().TrimEnd('/').Replace(".git", "");
        var parts = cleanUrl.Split('/');

        if (parts.Length >= 2)
        {
            string repo = parts[^1];
            string owner = parts[^2];
            return (owner, repo);
        }

        throw new ArgumentException($"Incorrect GitHub URL: {url}");
    }

    public async Task<bool> DownloadRepository(string owner, string repo, string savePath)
    {
        try
        {
            string url = $"/repos/{owner}/{repo}/zipball";
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Download error: {response.StatusCode}");
                return false;
            }

            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);

            using var downloadStream = await response.Content.ReadAsStreamAsync();

            await downloadStream.CopyToAsync(fileStream);

            return true;

        }
        catch (Exception ex)
        {
            Logger.Error($"Critical download error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UploadFile(string owner, string repo, string filePath, byte[] content)
    {
        try
        {
            string url = $"/repos/{owner}/{repo}/contents/{filePath}";
            string base64Content = Convert.ToBase64String(content);

            string? currentSha = null;

            var getResponse = await _client.GetAsync(url);
            if (getResponse.IsSuccessStatusCode)
            {
                var jsonString = await getResponse.Content.ReadAsStringAsync();
                var node = JsonNode.Parse(jsonString);
                currentSha = node?["sha"]?.ToString();
            }

            var body = new
            {
                message = $"Update profile {Path.GetFileName(filePath)} (via FikaSync)",
                content = base64Content,
                sha = currentSha
            };

            var response = await _client.PutAsJsonAsync(url, body);

            if (response.IsSuccessStatusCode)
            {
                Logger.Info($"[green]√[/] File sent: {Path.GetFileName(filePath)}");
                return true;
            }
            else
            {
                Logger.Error($"Sending error {filePath}: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Critical sending error: {ex.Message}");
            return false;
        }
    }

    public async Task<(string TagName, string HtmlUrl)?> GetLatestReleaseInfo(string repoName)
    {
        try
        {
            var response = await _client.GetAsync($"/repos/{repoName}/releases/latest");
            if (!response.IsSuccessStatusCode) return null;

            var jsonString = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(jsonString);

            string tag = node?["tag_name"]?.ToString() ?? "";
            string url = node?["html_url"]?.ToString() ?? "";

            return (tag, url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> DownloadFileContent(string owner, string repo, string filePath)
    {
        try
        {
            string url = $"/repos/{owner}/{repo}/contents/{filePath}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.raw"));

            var response = await _client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                Logger.Error($"Error downloading content for verification {filePath}: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Critical verification download error: {ex.Message}");
            return null;
        }
    }
}