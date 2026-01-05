using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StickyNotesClassic.Core.Data;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Services;
using System;
using System.Globalization;
using StickyNotesClassic.Core.Utilities;
using System.Text.Json;
using System.Threading;

namespace StickyNotesClassic.Core.Repositories;

/// <summary>
/// SQLite-based repository implementation for notes and settings.
/// </summary>
public class NotesRepository : INotesRepository
{
    private readonly NotesDbContext _dbContext;
    private readonly ILogger<NotesRepository> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public NotesRepository(NotesDbContext dbContext, ILogger<NotesRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<Note>> GetAllActiveNotesAsync()
    {
        await using var conn = await _dbContext.CreateConnectionAsync().ConfigureAwait(false);
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
        await using var conn = await _dbContext.CreateConnectionAsync().ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, created_utc, updated_utc, color, is_topmost, x, y, width, height,
                   content_rtf, content_text, is_deleted
            FROM notes
            WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
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

        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var conn = await _dbContext.CreateConnectionAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
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

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task SoftDeleteNoteAsync(string id)
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var conn = await _dbContext.CreateConnectionAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE notes
                SET is_deleted = 1, updated_utc = @updated_utc
                WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@updated_utc", DateTime.UtcNow.ToString("O"));

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        await using var conn = await _dbContext.CreateConnectionAsync().ConfigureAwait(false);
        var settings = new AppSettings();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        var settingsDict = new Dictionary<string, string>();
        
        while (await reader.ReadAsync())
        {
            settingsDict[reader.GetString(0)] = reader.GetString(1);
        }

        // Deserialize from key-value pairs with error handling
        try
        {
            if (settingsDict.TryGetValue("DefaultFontFamily", out var fontFamily))
                settings.DefaultFontFamily = fontFamily;
            if (settingsDict.TryGetValue("DefaultFontSize", out var fontSize))
                settings.DefaultFontSize = double.TryParse(fontSize, CultureInfo.InvariantCulture, out var size) 
                    ? size : settings.DefaultFontSize;
            if (settingsDict.TryGetValue("DefaultNoteColor", out var noteColor))
                settings.DefaultNoteColor = int.TryParse(noteColor, out var color) 
                    ? (NoteColor)color : settings.DefaultNoteColor;
            if (settingsDict.TryGetValue("EnableBackgroundGradient", out var bgGradient))
                settings.EnableBackgroundGradient = bool.TryParse(bgGradient, out var bg) ? bg : settings.EnableBackgroundGradient;
            if (settingsDict.TryGetValue("EnableEnhancedShadow", out var enhancedShadow))
                settings.EnableEnhancedShadow = bool.TryParse(enhancedShadow, out var shadow) ? shadow : settings.EnableEnhancedShadow;
            if (settingsDict.TryGetValue("EnableGlossyHeader", out var glossyHeader))
                settings.EnableGlossyHeader = bool.TryParse(glossyHeader, out var glossy) ? glossy : settings.EnableGlossyHeader;
            if (settingsDict.TryGetValue("EnableTextShadow", out var textShadow))
                settings.EnableTextShadow = bool.TryParse(textShadow, out var ts) ? ts : settings.EnableTextShadow;
            if (settingsDict.TryGetValue("AskBeforeClose", out var askBeforeClose))
                settings.AskBeforeClose = bool.TryParse(askBeforeClose, out var ask) ? ask : settings.AskBeforeClose;
            if (settingsDict.TryGetValue("HotkeyModifiers", out var modifiers))
                settings.HotkeyModifiers = modifiers;
            if (settingsDict.TryGetValue("HotkeyKey", out var key))
                settings.HotkeyKey = key;
            if (settingsDict.TryGetValue("AutoBackupEnabled", out var backupEnabled))
                settings.AutoBackupEnabled = bool.TryParse(backupEnabled, out var enabled) ? enabled : settings.AutoBackupEnabled;
            if (settingsDict.TryGetValue("AutoBackupRetentionDays", out var retentionDays))
                settings.AutoBackupRetentionDays = int.TryParse(retentionDays, out var days) ? days : settings.AutoBackupRetentionDays;
            if (settingsDict.TryGetValue("AutoBackupRetentionCount", out var retentionCount))
                settings.AutoBackupRetentionCount = int.TryParse(retentionCount, out var count) ? count : settings.AutoBackupRetentionCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing settings, using defaults");
        }

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        await using var conn = await _dbContext.CreateConnectionAsync().ConfigureAwait(false);

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
            { "AskBeforeClose", settings.AskBeforeClose.ToString() },
            { "HotkeyModifiers", settings.HotkeyModifiers },
            { "HotkeyKey", settings.HotkeyKey },
            { "AutoBackupEnabled", settings.AutoBackupEnabled.ToString() },
            { "AutoBackupRetentionDays", settings.AutoBackupRetentionDays.ToString() },
            { "AutoBackupRetentionCount", settings.AutoBackupRetentionCount.ToString() }
        };

        await using var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false) as SqliteTransaction
            ?? throw new InvalidOperationException("Failed to open SQLite transaction for settings save.");
        try
        {
            foreach (var kvp in settingsDict)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO settings (key, value)
                    VALUES (@key, @value)
                    ON CONFLICT(key) DO UPDATE SET value = @value;";
                cmd.Parameters.AddWithValue("@key", kvp.Key);
                cmd.Parameters.AddWithValue("@value", kvp.Value);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await transaction.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            await transaction.RollbackAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<int> UpdateAllNoteFontsAsync(string fontFamily, double fontSize)
    {
        var fontValidation = ValidationService.ValidateFontFamily(fontFamily);
        if (!fontValidation.IsValid)
        {
            throw new ArgumentException(fontValidation.ErrorMessage);
        }

        var clampedSize = Math.Clamp(fontSize, 8, 72);
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var conn = await _dbContext.CreateConnectionAsync().ConfigureAwait(false);
            var updatedCount = 0;

            await using var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false) as SqliteTransaction
                ?? throw new InvalidOperationException("Failed to open SQLite transaction for font update.");
            try
            {
                await using var selectCmd = conn.CreateCommand();
                selectCmd.Transaction = transaction;
                selectCmd.CommandText = @"SELECT id, content_text FROM notes WHERE is_deleted = 0;";

                var updates = new List<(string Id, string ContentRtf)>();

                await using (var reader = await selectCmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var id = reader.GetString(0);
                        var contentText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var rtf = RtfHelper.BuildRtf(contentText, fontFamily, clampedSize);
                        updates.Add((id, rtf));
                    }
                }

                foreach (var update in updates)
                {
                    await using var updateCmd = conn.CreateCommand();
                    updateCmd.Transaction = transaction;
                    updateCmd.CommandText = @"UPDATE notes SET content_rtf = @rtf, updated_utc = @updated WHERE id = @id;";
                    updateCmd.Parameters.AddWithValue("@rtf", update.ContentRtf);
                    updateCmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("O"));
                    updateCmd.Parameters.AddWithValue("@id", update.Id);

                    updatedCount += await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                await transaction.CommitAsync().ConfigureAwait(false);
                _logger.LogInformation("Updated fonts for {Count} note(s) to {FontFamily} {FontSize}", updatedCount, fontFamily, clampedSize);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                _logger.LogError(ex, "Failed to update fonts for all notes");
                throw;
            }

            return updatedCount;
        }
        finally
        {
            _writeGate.Release();
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
