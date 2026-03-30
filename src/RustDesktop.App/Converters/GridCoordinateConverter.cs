using System;
using System.Globalization;
using System.Windows.Data;

namespace RustDesktop.App.Converters;

/// <summary>
/// Converts Rust world coordinates (X, Y) to grid format (e.g., A1, F24)
/// Rust maps use a dynamic grid: cells = worldSize / 150 (rounded)
/// </summary>
public class GridCoordinateConverter : IMultiValueConverter
{
    // Rust maps typically have a world size of 4500 units
    // Grid cells are 150 world units each
    private const float DefaultWorldSize = 4500f;
    private const float CellSize = 150f;
    
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return string.Empty;
        
        // Handle different numeric types
        float xCoord = 0f;
        float yCoord = 0f;
        
        if (values[0] is float xf)
            xCoord = xf;
        else if (values[0] is double xd)
            xCoord = (float)xd;
        else if (values[0] is int xi)
            xCoord = xi;
        else
            return string.Empty;
        
        if (values[1] is float yf)
            yCoord = yf;
        else if (values[1] is double yd)
            yCoord = (float)yd;
        else if (values[1] is int yi)
            yCoord = yi;
        else
            return string.Empty;
        
        // Get world size if provided (optional third parameter)
        float worldSize = DefaultWorldSize;
        if (values.Length >= 3 && values[2] != null)
        {
            if (values[2] is float ws)
                worldSize = ws;
            else if (values[2] is int wsInt)
                worldSize = wsInt;
        }
        
        if (worldSize <= 0) return string.Empty;
        
        // Rust grid calculation: cells = round(worldSize / 150)
        // Each cell is worldSize / cells units
        // Use double for precision to match reference implementation exactly
        double worldSizeD = worldSize;
        int cells = Math.Max(1, (int)Math.Round(worldSizeD / CellSize));
        double cell = worldSizeD / cells;
        
        // Rust grid labels are derived directly from world coordinates and map size.
        int col = Math.Clamp((int)Math.Floor(xCoord / cell), 0, cells - 1);
        int row = Math.Clamp((int)Math.Floor((worldSizeD - yCoord) / cell), 0, cells - 1);
        
        // Convert to grid format: Letter(s) + Number
        string letter = ColumnLabel(col);
        // Row numbers: Reference shows 0-based internally, but Rust displays 1-based (A1, A2, etc.)
        // We add 1 to convert from 0-based to 1-based for display
        int number = row + 1;
        return $"{letter}{number}";
    }
    
    private static string ColumnLabel(int index)
    {
        // Convert column index to letter(s): 0 -> A, 25 -> Z, 26 -> AA, etc.
        // This matches the reference implementation exactly
        var s = "";
        index++; // Convert to 1-based for calculation
        while (index > 0)
        {
            index--;
            s = (char)('A' + (index % 26)) + s;
            index /= 26;
        }
        return s;
    }
    
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Single value converter that takes a VendingMachine and returns its grid coordinate
/// </summary>
public class VendingMachineGridConverter : IValueConverter
{
    private const float DefaultWorldSize = 4500f;
    private const float CellSize = 150f;
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Core.Models.VendingMachine vm)
            return string.Empty;
        
        float worldSize = DefaultWorldSize;
        if (parameter is float ws)
            worldSize = ws;
        else if (parameter is int wsInt)
            worldSize = wsInt;
        
        if (worldSize <= 0) return string.Empty;
        
        // Rust grid calculation: cells = round(worldSize / 150)
        // Use double for precision to match reference implementation exactly
        double worldSizeD = worldSize;
        int cells = Math.Max(1, (int)Math.Round(worldSizeD / CellSize));
        double cell = worldSizeD / cells;
        
        // Rust grid labels are derived directly from world coordinates and map size.
        int col = Math.Clamp((int)Math.Floor(vm.X / cell), 0, cells - 1);
        int row = Math.Clamp((int)Math.Floor((worldSizeD - vm.Y) / cell), 0, cells - 1);
        
        // Convert to grid format
        string letter = ColumnLabel(col);
        // Row numbers: Reference shows 0-based, but Rust displays 1-based (A1, A2, etc.)
        int number = row + 1;
        return $"{letter}{number}";
    }
    
    private static string ColumnLabel(int index)
    {
        var s = "";
        index++;
        while (index > 0)
        {
            index--;
            s = (char)('A' + (index % 26)) + s;
            index /= 26;
        }
        return s;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


