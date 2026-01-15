using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace RustDesktop.Core.Services;

public class UpdateService : IUpdateService
{
    private readonly ILoggingService? _logger;
    private readonly HttpClient _httpClient;
    private const string UpdateMarkerFile = "update-pending.txt";

    public UpdateService(ILoggingService? logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(string updateUrl)
    {
        try
        {
            _logger?.LogInfo($"Checking for updates from: {updateUrl}");
            
            // Check if this is a GitHub API URL
            if (updateUrl.Contains("api.github.com/repos"))
            {
                return await CheckGitHubReleaseAsync(updateUrl);
            }
            
            // Otherwise, use custom JSON format
            var response = await _httpClient.GetStringAsync(updateUrl);
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (updateInfo != null)
            {
                var currentVersion = GetCurrentVersion();
                _logger?.LogInfo($"Current version: {currentVersion}, Latest version: {updateInfo.Version}");
                
                if (IsNewerVersion(updateInfo.Version, currentVersion))
                {
                    _logger?.LogInfo($"Update available: {updateInfo.Version}");
                    return updateInfo;
                }
                else
                {
                    _logger?.LogInfo("Already on latest version");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error checking for updates: {ex.Message}", ex);
            return null;
        }
    }

    private async Task<UpdateInfo?> CheckGitHubReleaseAsync(string githubApiUrl)
    {
        try
        {
            // Add User-Agent header (required by GitHub API)
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RustDesktop-UpdateChecker/1.0");
            
            var response = await _httpClient.GetStringAsync(githubApiUrl);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            // Parse GitHub release format
            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var version = tagName.TrimStart('v'); // Remove 'v' prefix if present (e.g., "v1.0.1" -> "1.0.1")
            var releaseNotes = root.GetProperty("body").GetString() ?? "";
            var isPrerelease = root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean();
            
            // Skip prereleases
            if (isPrerelease)
            {
                _logger?.LogInfo($"Latest release is a prerelease, skipping: {version}");
                return null;
            }

            // Find the Windows ZIP asset
            var assets = root.GetProperty("assets");
            string? downloadUrl = null;
            long fileSize = 0;
            
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("win-x64") && name.EndsWith(".zip"))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    fileSize = asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0;
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                _logger?.LogWarning("No Windows ZIP asset found in GitHub release");
                return null;
            }

            var currentVersion = GetCurrentVersion();
            _logger?.LogInfo($"Current version: {currentVersion}, Latest GitHub release: {version}");
            
            if (IsNewerVersion(version, currentVersion))
            {
                _logger?.LogInfo($"Update available from GitHub: {version}");
                return new UpdateInfo
                {
                    Version = version,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes,
                    IsMandatory = false,
                    FileSize = fileSize
                };
            }
            else
            {
                _logger?.LogInfo("Already on latest version");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error checking GitHub release: {ex.Message}", ex);
            return null;
        }
    }

    public async Task<bool> DownloadUpdateAsync(string downloadUrl, string targetPath, IProgress<double>? progress = null)
    {
        try
        {
            _logger?.LogInfo($"Downloading update from: {downloadUrl}");
            
            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var contentStream = await response.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var percentage = (double)downloadedBytes / totalBytes * 100;
                        progress.Report(percentage);
                    }
                }
            }

            _logger?.LogInfo($"Update downloaded successfully to: {targetPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error downloading update: {ex.Message}", ex);
            return false;
        }
    }

    public void ScheduleUpdateInstallation(string newVersionPath)
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var markerPath = Path.Combine(appDir, UpdateMarkerFile);
            
            // Write the path to the new version file
            File.WriteAllText(markerPath, newVersionPath);
            
            _logger?.LogInfo($"Update installation scheduled. New version: {newVersionPath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error scheduling update installation: {ex.Message}", ex);
        }
    }

    private string GetCurrentVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0.0";
        }
        catch
        {
            return "1.0.0.0";
        }
    }

    private bool IsNewerVersion(string newVersion, string currentVersion)
    {
        try
        {
            var newVer = new Version(newVersion);
            var currentVer = new Version(currentVersion);
            return newVer > currentVer;
        }
        catch
        {
            // If version parsing fails, assume it's newer to be safe
            return true;
        }
    }
}
