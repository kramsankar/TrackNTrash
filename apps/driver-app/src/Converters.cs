using System.Globalization;

namespace TrackNTrash.DriverApp;

/// <summary>Red when the banner reports an error (wrong trip / locked), green on success.</summary>
public sealed class BannerColorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isError = values.Length > 0 && values[0] is bool b && b;
        return isError ? Color.FromArgb("#C81E1E") : Color.FromArgb("#1E8E3E");
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>✓ when a tray is loaded, ○ otherwise.</summary>
public sealed class LoadedTickConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "✅" : "○";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
