namespace StickyNotesClassic.Core.Models;

/// <summary>
/// Domain model representing a sticky note with its content, formatting, and window state.
/// </summary>
public class Note
{
    // Minimum and default dimensions from spec (DIP)
    public const double MinWidth = 140;
    public const double MinHeight = 120;
    public const double DefaultWidth = 220;
    public const double DefaultHeight = 200;

    /// <summary>
    /// Unique identifier for the note.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the note was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the note was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Color theme of the note.
    /// </summary>
    public NoteColor Color { get; set; }

    /// <summary>
    /// Whether the note window is always on top.
    /// </summary>
    public bool IsTopmost { get; set; }

    /// <summary>
    /// Window X position.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Window Y position.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Window width.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Window height.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Formatted content stored as RTF.
    /// </summary>
    public string ContentRtf { get; set; } = string.Empty;

    /// <summary>
    /// Plain text cache for search/indexing.
    /// </summary>
    public string ContentText { get; set; } = string.Empty;

    /// <summary>
    /// Soft delete flag (allows undo later).
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Validates and clamps the note's dimensions to spec constraints.
    /// </summary>
    public void ValidateBounds()
    {
        Width = Math.Max(MinWidth, Width);
        Height = Math.Max(MinHeight, Height);
    }

    /// <summary>
    /// Creates a new note with default values.
    /// </summary>
    public static Note CreateNew(NoteColor color = NoteColor.Yellow)
    {
        var now = DateTime.UtcNow;
        return new Note
        {
            Id = Guid.NewGuid().ToString(),
            CreatedUtc = now,
            UpdatedUtc = now,
            Color = color,
            IsTopmost = false,
            X = 100, // Default position, will be adjusted to avoid overlap
            Y = 100,
            Width = DefaultWidth,
            Height = DefaultHeight,
            ContentRtf = string.Empty,
            ContentText = string.Empty,
            IsDeleted = false
        };
    }
}
