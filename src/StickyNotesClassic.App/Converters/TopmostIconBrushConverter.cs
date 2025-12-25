using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace StickyNotesClassic.App.Converters;

/// <summary>
/// Converter for topmost icon brush (filled when true, outlined when false).
/// </summary>
public class TopmostIconBrushConverter : IValueConverter
{
    public static readonly TopmostIconBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isTopmost)
        {
            return isTopmost ? Brushes.DarkOrange : Brush.Parse("#555555");
        }
        return Brush.Parse("#555555");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
