using Avalonia.Data.Converters;
using StickyNotesClassic.Core.Models;
using System;
using System.Globalization;

namespace StickyNotesClassic.App.Converters;

/// <summary>
/// Converts integer to NoteColor enum for MenuItem CommandParameter binding.
/// </summary>
public class IntToNoteColorConverter : IValueConverter
{
    public static readonly IntToNoteColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && Enum.IsDefined(typeof(NoteColor), intValue))
        {
            return (NoteColor)intValue;
        }
        return NoteColor.Yellow; // Default
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NoteColor color)
        {
            return (int)color;
        }
        return 0;
    }
}
