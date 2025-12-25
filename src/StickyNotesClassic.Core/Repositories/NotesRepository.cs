using Microsoft.Data.Sqlite;
using StickyNotesClassic.Core.Data;
using StickyNotesClassic.Core.Models;
using System.Globalization;
using System.Text.Json;

namespace StickyNotesClassic.Core.Repositories;

/// <summary>
/// SQLite-based repository implementation for notes and settings.
/// </summary>
public class NotesRepository : INotesRepository
{
    private readonly NotesDbContext _dbContext;

    public NotesRepository(NotesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Note>> GetAllActiveNotesAsync()
    {
        var conn = _dbContext.GetConnection();
        var notes = new List<Note>();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, created_utc, updated_utc, color, is_topmost, x, y, width, height, 
                   content_rtf, content_text, is_deleted
            FROM notes 
            WHERE is_deleted = 0
            ORDER BY updated_utc DESC;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notes.Add(MapNoteFromReader(reader));
        }

        return notes;
    }

    public async Task<Note?> GetNoteByIdAsync(string id)
    {
        var conn = _dbContext.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, created_utc, updated_utc, color, is_topmost, x, y, width, height, 
                   content_rtf, content_text, is_deleted
            FROM notes 
            WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapNoteFromReader(reader);
        }

        return null;
    }

    public async Task UpsertNoteAsync(Note note)
    {
        note.UpdatedUtc = DateTime.UtcNow;
        note.ValidateBounds();

        var conn = _dbContext.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO notes (id, created_utc, updated_utc, color, is_topmost, x, y, width, height, 
                              content_rtf, content_text, is_deleted)
            VALUES (@id, @created_utc, @updated_utc, @color, @is_topmost, @x, @y, @width, @height, 
                    @content_rtf, @content_text, @is_deleted)
            ON CONFLICT(id) DO UPDATE SET
                updated_utc = @updated_utc,
                color = @color,
                is_topmost = @is_topmost,
                x = @x,
                y = @y,
                width = @width,
                height = @height,
                content_rtf = @content_rtf,
                content_text = @content_text,
                is_deleted = @is_deleted;";

        cmd.Parameters.AddWithValue("@id", note.Id);
        cmd.Parameters.AddWithValue("@created_utc", note.CreatedUtc.ToString("O")); // ISO 8601
        cmd.Parameters.AddWithValue("@updated_utc", note.UpdatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("@color", (int)note.Color);
        cmd.Parameters.AddWithValue("@is_topmost", note.IsTopmost ? 1 : 0);
        cmd.Parameters.AddWithValue("@x", note.X);
        cmd.Parameters.AddWithValue("@y", note.Y);
        cmd.Parameters.AddWithValue("@width", note.Width);
        cmd.Parameters.AddWithValue("@height", note.Height);
        cmd.Parameters.AddWithValue("@content_rtf", note.ContentRtf);
        cmd.Parameters.AddWithValue("@content_text", note.ContentText);
        cmd.Parameters.AddWithValue("@is_deleted", note.IsDeleted ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SoftDeleteNoteAsync(string id)
    {
        var conn = _dbContext.GetConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE notes 
            SET is_deleted = 1, updated_utc = @updated_utc 
            WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@updated_utc", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        var conn = _dbContext.GetConnection();
        var settings = new AppSettings();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";

        using var reader = await cmd.ExecuteReaderAsync();
        var settingsDict = new Dictionary<string, string>();
        
        while (await reader.ReadAsync())
        {
            settingsDict[reader.GetString(0)] = reader.GetString(1);
        }

        // Deserialize from key-value pairs
        if (settingsDict.TryGetValue("DefaultFontFamily", out var fontFamily))
            settings.DefaultFontFamily = fontFamily;
        if (settingsDict.TryGetValue("DefaultFontSize", out var fontSize))
            settings.DefaultFontSize = double.Parse(fontSize, CultureInfo.InvariantCulture);
        if (settingsDict.TryGetValue("DefaultNoteColor", out var noteColor))
            settings.DefaultNoteColor = (NoteColor)int.Parse(noteColor);
        if (settingsDict.TryGetValue("EnableBackgroundGradient", out var bgGradient))
            settings.EnableBackgroundGradient = bool.Parse(bgGradient);
        if (settingsDict.TryGetValue("EnableEnhancedShadow", out var enhancedShadow))
            settings.EnableEnhancedShadow = bool.Parse(enhancedShadow);
        if (settingsDict.TryGetValue("EnableGlossyHeader", out var glossyHeader))
            settings.EnableGlossyHeader = bool.Parse(glossyHeader);
        if (settingsDict.TryGetValue("EnableTextShadow", out var textShadow))
            settings.EnableTextShadow = bool.Parse(textShadow);
        if (settingsDict.TryGetValue("HotkeyModifiers", out var modifiers))
            settings.HotkeyModifiers = modifiers;
        if (settingsDict.TryGetValue("HotkeyKey", out var key))
            settings.HotkeyKey = key;
        if (settingsDict.TryGetValue("AutoBackupEnabled", out var backupEnabled))
            settings.AutoBackupEnabled = bool.Parse(backupEnabled);
        if (settingsDict.TryGetValue("AutoBackupRetentionDays", out var retentionDays))
            settings.AutoBackupRetentionDays = int.Parse(retentionDays);

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var conn = _dbContext.GetConnection();
        
        // Serialize to key-value pairs
        var settingsDict = new Dictionary<string, string>
        {
            { "DefaultFontFamily", settings.DefaultFontFamily },
            { "DefaultFontSize", settings.DefaultFontSize.ToString(CultureInfo.InvariantCulture) },
            { "DefaultNoteColor", ((int)settings.DefaultNoteColor).ToString() },
            { "EnableBackgroundGradient", settings.EnableBackgroundGradient.ToString() },
            { "EnableEnhancedShadow", settings.EnableEnhancedShadow.ToString() },
            { "EnableGlossyHeader", settings.EnableGlossyHeader.ToString() },
            { "EnableTextShadow", settings.EnableTextShadow.ToString() },
            { "HotkeyModifiers", settings.HotkeyModifiers },
            { "HotkeyKey", settings.HotkeyKey },
            { "AutoBackupEnabled", settings.AutoBackupEnabled.ToString() },
            { "AutoBackupRetentionDays", settings.AutoBackupRetentionDays.ToString() }
        };

        using var transaction = conn.BeginTransaction();
        try
        {
            foreach (var kvp in settingsDict)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO settings (key, value) 
                    VALUES (@key, @value)
                    ON CONFLICT(key) DO UPDATE SET value = @value;";
                cmd.Parameters.AddWithValue("@key", kvp.Key);
                cmd.Parameters.AddWithValue("@value", kvp.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static Note MapNoteFromReader(SqliteDataReader reader)
    {
        return new Note
        {
            Id = reader.GetString(0),
            CreatedUtc = DateTime.Parse(reader.GetString(1), null, DateTimeStyles.RoundtripKind),
            UpdatedUtc = DateTime.Parse(reader.GetString(2), null, DateTimeStyles.RoundtripKind),
            Color = (NoteColor)reader.GetInt32(3),
            IsTopmost = reader.GetInt32(4) != 0,
            X = reader.GetDouble(5),
            Y = reader.GetDouble(6),
            Width = reader.GetDouble(7),
            Height = reader.GetDouble(8),
            ContentRtf = reader.GetString(9),
            ContentText = reader.GetString(10),
            IsDeleted = reader.GetInt32(11) != 0
        };
    }
}
