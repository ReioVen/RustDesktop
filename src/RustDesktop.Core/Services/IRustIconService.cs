namespace RustDesktop.Core.Services;

public interface IRustIconService
{
    string? GetItemIconPath(int itemId, string? shortName = null);
    string? GetItemIconPath(string? shortName);
    string? GetRustInstallPath();
    bool IsRustInstalled { get; }
}












