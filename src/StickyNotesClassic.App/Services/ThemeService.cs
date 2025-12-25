using Avalonia.Media;
using StickyNotesClassic.Core.Models;
using System.Collections.Generic;

namespace StickyNotesClassic.App.Services;

/// <summary>
/// Service for managing note color themes.
/// Provides brushes for each color palette.
/// </summary>
public class ThemeService
{
    private readonly Dictionary<NoteColor, ThemeBrushes> _themes;

    public ThemeService()
    {
        _themes = new Dictionary<NoteColor, ThemeBrushes>
        {
            {
                NoteColor.Yellow, new ThemeBrushes
                {
                    Fill = Brush.Parse("#FEFF9C"),
                    FillHighlight = Brush.Parse("#FFFCE0"),
                    FillDark = Brush.Parse("#E6E68C"),
                    Border = Brush.Parse("#D6D37A")
                }
            },
            {
                NoteColor.Blue, new ThemeBrushes
                {
                    Fill = Brush.Parse("#7AFCFF"),
                    FillHighlight = Brush.Parse("#DFFBFF"),
                    FillDark = Brush.Parse("#6EE3E6"),
                    Border = Brush.Parse("#6BC9CC")
                }
            },
            {
                NoteColor.Pink, new ThemeBrushes
                {
                    Fill = Brush.Parse("#FF7EB9"),
                    FillHighlight = Brush.Parse("#FFE0EF"),
                    FillDark = Brush.Parse("#E671A7"),
                    Border = Brush.Parse("#D46A97")
                }
            },
            {
                NoteColor.Green, new ThemeBrushes
                {
                    Fill = Brush.Parse("#B8FF9C"),
                    FillHighlight = Brush.Parse("#E6FFE0"),
                    FillDark = Brush.Parse("#A6E68C"),
                    Border = Brush.Parse("#86C26F")
                }
            },
            {
                NoteColor.Purple, new ThemeBrushes
                {
                    Fill = Brush.Parse("#C5B3FF"),
                    FillHighlight = Brush.Parse("#EEE8FF"),
                    FillDark = Brush.Parse("#B1A1E6"),
                    Border = Brush.Parse("#9A88D6")
                }
            },
            {
                NoteColor.White, new ThemeBrushes
                {
                    Fill = Brush.Parse("#FFFFFF"),
                    FillHighlight = Brush.Parse("#FFFFFF"),
                    FillDark = Brush.Parse("#F0F0F0"),
                    Border = Brush.Parse("#CFCFCF")
                }
            }
        };
    }

    /// <summary>
    /// Gets theme brushes for a specific color.
    /// </summary>
    public ThemeBrushes GetTheme(NoteColor color)
    {
        return _themes.TryGetValue(color, out var theme) 
            ? theme 
            : _themes[NoteColor.Yellow]; // Default to yellow
    }
}

/// <summary>
/// Brushes for a note theme.
/// </summary>
public class ThemeBrushes
{
    public IBrush Fill { get; set; } = Brushes.Transparent;
    public IBrush FillHighlight { get; set; } = Brushes.Transparent;
    public IBrush FillDark { get; set; } = Brushes.Transparent;
    public IBrush Border { get; set; } = Brushes.Transparent;
}
