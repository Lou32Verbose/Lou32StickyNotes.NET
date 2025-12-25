namespace StickyNotesClassic.Core.Models;

/// <summary>
/// Application-wide settings and preferences.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Default font family for new notes.
    /// </summary>
    public string DefaultFontFamily { get; set; } = "Segoe Print";

    /// <summary>
    /// Default font size for new notes (in DIP).
    /// </summary>
    public double DefaultFontSize { get; set; } = 12.0;

    /// <summary>
    /// Global hotkey modifier keys (serialized as comma-separated string: e.g., "Control,Alt").
    /// </summary>
    public string HotkeyModifiers { get; set; } = "Control,Alt";

    /// <summary>
    /// Global hotkey key (e.g., "N").
    /// </summary>
    public string HotkeyKey { get; set; } = "N";

    /// <summary>
    /// Whether automatic daily backups are enabled.
    /// </summary>
    public bool AutoBackupEnabled { get; set; } = true;

    /// <summary>
    /// Number of days to retain automatic backups.
    /// </summary>
    public int AutoBackupRetentionDays { get; set; } = 7;

    /// <summary>
    /// Default color for newly created notes.
    /// </summary>
    public NoteColor DefaultNoteColor { get; set; } = NoteColor.Yellow;

    /// <summary>
    /// Enable vertical gradient on note backgrounds (Windows 7 style).
    /// </summary>
    public bool EnableBackgroundGradient { get; set; } = true;

    /// <summary>
    /// Enable enhanced soft shadow effect.
    /// </summary>
    public bool EnableEnhancedShadow { get; set; } = true;

    /// <summary>
    /// Enable glossy gradient on header strip.
    /// </summary>
    public bool EnableGlossyHeader { get; set; } = true;

    /// <summary>
    /// Enable subtle text shadow on darker note colors.
    /// </summary>
    public bool EnableTextShadow { get; set; } = false;
}
