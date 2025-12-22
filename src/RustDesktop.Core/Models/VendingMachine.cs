using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RustDesktop.Core.Models;

public class VendingMachine
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public List<VendingItem> Items { get; set; } = new();
    public List<VendingItem> BuyItems { get; set; } = new();
    public bool IsActive { get; set; }
}

public class VendingItem : INotifyPropertyChanged
{
    private int _itemId;
    private string _itemName = string.Empty;
    private string? _shortName;
    private string? _iconUrl;
    private int _quantity;
    private int _cost;
    private string _currency = "scrap";
    private int _currencyItemId;
    private string _currencyItemName = "Scrap";
    private string? _currencyIconUrl;

    public int ItemId
    {
        get => _itemId;
        set { _itemId = value; OnPropertyChanged(); }
    }

    public string ItemName
    {
        get => _itemName;
        set { _itemName = value; OnPropertyChanged(); }
    }

    public string? ShortName
    {
        get => _shortName;
        set { _shortName = value; OnPropertyChanged(); }
    }

    public string? IconUrl
    {
        get => _iconUrl;
        set 
        { 
            _iconUrl = value;
            System.Diagnostics.Debug.WriteLine($"[VendingItem] IconUrl set to: {value ?? "null"}");
            OnPropertyChanged(); 
        }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = value; OnPropertyChanged(); }
    }

    public int Cost
    {
        get => _cost;
        set { _cost = value; OnPropertyChanged(); }
    }

    public string Currency
    {
        get => _currency;
        set { _currency = value; OnPropertyChanged(); }
    }

    public int CurrencyItemId
    {
        get => _currencyItemId;
        set { _currencyItemId = value; OnPropertyChanged(); }
    }

    public string CurrencyItemName
    {
        get => _currencyItemName;
        set { _currencyItemName = value; OnPropertyChanged(); }
    }

    public string? CurrencyIconUrl
    {
        get => _currencyIconUrl;
        set { _currencyIconUrl = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}








