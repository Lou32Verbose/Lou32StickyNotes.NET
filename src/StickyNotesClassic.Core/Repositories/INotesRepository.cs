using StickyNotesClassic.Core.Models;

namespace StickyNotesClassic.Core.Repositories;

/// <summary>
/// Repository interface for note persistence operations.
/// </summary>
public interface INotesRepository
{
    /// <summary>
    /// Gets all active (non-deleted) notes.
    /// </summary>
    Task<List<Note>> GetAllActiveNotesAsync();

    /// <summary>
    /// Gets a specific note by ID.
    /// </summary>
    Task<Note?> GetNoteByIdAsync(string id);

    /// <summary>
    /// Inserts or updates a note.
    /// </summary>
    Task UpsertNoteAsync(Note note);

    /// <summary>
    /// Soft deletes a note (marks as deleted without removing from database).
    /// </summary>
    Task SoftDeleteNoteAsync(string id);

    /// <summary>
    /// Gets application settings.
    /// </summary>
    Task<AppSettings> GetSettingsAsync();

    /// <summary>
    /// Saves application settings.
    /// </summary>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Rewrites stored note content to use the provided font settings.
    /// </summary>
    Task<int> UpdateAllNoteFontsAsync(string fontFamily, double fontSize);
}
