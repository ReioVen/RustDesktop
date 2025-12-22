namespace RustDesktop.Core.Services;

public interface IItemNameService
{
    string GetItemName(int itemId, string? shortName = null);
    string GetItemName(string? shortName);
    string? GetShortName(int itemId);
    string? GetItemIconUrl(int itemId, string? shortName = null);
    string? GetItemIconUrl(string? shortName);
    string? GetItemIconPath(int itemId, string? shortName = null);
    string? GetItemIconPath(string? shortName);
    void LoadItemMappings();
    bool IsLoaded { get; }
}






