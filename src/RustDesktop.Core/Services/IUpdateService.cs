namespace RustDesktop.Core.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdatesAsync(string updateUrl);
    Task<bool> DownloadUpdateAsync(string downloadUrl, string targetPath, IProgress<double>? progress = null);
    void ScheduleUpdateInstallation(string newVersionPath);
}

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public long FileSize { get; set; }
}
