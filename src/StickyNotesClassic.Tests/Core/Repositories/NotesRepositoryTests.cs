using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using StickyNotesClassic.Core.Data;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace StickyNotesClassic.Tests.Core.Repositories;

public class NotesRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly NotesDbContext _dbContext;
    private readonly NotesRepository _repository;

    public NotesRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"notes_repo_{Guid.NewGuid():N}.db");
        _dbContext = new NotesDbContext(_dbPath);
        _dbContext.InitializeAsync().GetAwaiter().GetResult();
        _repository = new NotesRepository(_dbContext, NullLogger<NotesRepository>.Instance);
    }

    [Fact]
    public async Task UpdateAllNoteFontsAsync_RewritesRtfWithFontAndSize()
    {
        var note = Note.CreateNew();
        note.ContentText = "Hello world";
        await _repository.UpsertNoteAsync(note);

        var updated = await _repository.UpdateAllNoteFontsAsync("Arial", 14);

        Assert.Equal(1, updated);

        var saved = await _repository.GetNoteByIdAsync(note.Id);
        Assert.NotNull(saved);
        Assert.Contains(@"{\rtf1", saved!.ContentRtf);
        Assert.Contains("Arial", saved.ContentRtf);
        Assert.Contains("\\fs28", saved.ContentRtf);
    }

    [Fact]
    public async Task Initialize_UpgradesPlainTextNotesToRtf()
    {
        // Seed a legacy database with plain text stored in the RTF column
        var id = Guid.NewGuid().ToString();
        var legacyPath = Path.Combine(Path.GetTempPath(), $"legacy_rtf_{Guid.NewGuid():N}.db");

        await using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyPath }.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY);
                CREATE TABLE IF NOT EXISTS notes (
                    id TEXT PRIMARY KEY,
                    created_utc TEXT NOT NULL,
                    updated_utc TEXT NOT NULL,
                    color INTEGER NOT NULL,
                    is_topmost INTEGER NOT NULL,
                    x REAL NOT NULL,
                    y REAL NOT NULL,
                    width REAL NOT NULL,
                    height REAL NOT NULL,
                    content_rtf TEXT NOT NULL,
                    content_text TEXT NOT NULL,
                    is_deleted INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO schema_version(version) VALUES (1);
                INSERT INTO notes(id, created_utc, updated_utc, color, is_topmost, x, y, width, height, content_rtf, content_text, is_deleted)
                VALUES ($id, $created, $created, 0, 0, 0, 0, 200, 200, 'legacy plain text', 'legacy plain text', 0);
            ";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        var legacyContext = new NotesDbContext(legacyPath);
        await legacyContext.InitializeAsync();
        var legacyRepo = new NotesRepository(legacyContext, NullLogger<NotesRepository>.Instance);

        var migrated = await legacyRepo.GetNoteByIdAsync(id);

        Assert.NotNull(migrated);
        Assert.StartsWith("{\\rtf", migrated!.ContentRtf);
        Assert.Equal("legacy plain text", migrated.ContentText);

        legacyContext.Dispose();
        if (File.Exists(legacyPath))
        {
            File.Delete(legacyPath);
        }
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
