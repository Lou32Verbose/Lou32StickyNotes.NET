using Microsoft.Extensions.Logging.Abstractions;
using StickyNotesClassic.App.Services;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using Xunit;

namespace StickyNotesClassic.Tests.App;

public class BackupServiceTests
{
    [Fact]
    public async Task ExportNotesAsync_WritesFileAndReportsCount()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var notes = new List<Note>
        {
            new() { Id = "1", ContentRtf = "{\\rtf1 ", ContentText = "First", CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow },
            new() { Id = "2", ContentRtf = "{\\rtf1 ", ContentText = "Second", CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow }
        };

        var repository = new FakeNotesRepository(notes);
        var service = new BackupService(repository, NullLogger<BackupService>.Instance, tempDir);

        var targetPath = Path.Combine(tempDir, "export.json");
        var result = await service.ExportNotesAsync(targetPath);

        Assert.Equal(notes.Count, result.ExportedCount);
        Assert.True(File.Exists(targetPath));

        var json = await File.ReadAllTextAsync(targetPath);
        var exported = JsonSerializer.Deserialize<BackupEnvelope>(json);
        Assert.Equal(notes.Count, exported?.Notes?.Count);
        Assert.False(string.IsNullOrWhiteSpace(exported?.Checksum));
    }

    [Fact]
    public async Task ImportNotesAsync_SkipsInvalidRtfAndReportsFailures()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var validNote = new Note
        {
            Id = "1",
            ContentRtf = "{\\rtf1\\ansi\\deff0 {\\fonttbl {\\f0 Arial;}} Hello}",
            ContentText = "Hello",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        var invalidNote = new Note
        {
            Id = "2",
            ContentRtf = "not-rtf",
            ContentText = "Broken",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        var importPath = Path.Combine(tempDir, "import.json");
        var checksumPayload = JsonSerializer.Serialize(new List<Note> { validNote, invalidNote });
        var checksum = ComputeChecksum(checksumPayload);
        var payload = new BackupEnvelope
        {
            Notes = new List<Note> { validNote, invalidNote },
            Checksum = checksum
        };

        File.WriteAllText(importPath, JsonSerializer.Serialize(payload));

        var repository = new FakeNotesRepository();
        var service = new BackupService(repository, NullLogger<BackupService>.Instance, tempDir);

        var result = await service.ImportNotesAsync(importPath);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Single(repository.Notes);
        Assert.Equal("Hello", repository.Notes.First().ContentText);
    }

    [Fact]
    public async Task ImportNotesAsync_RejectsTamperedChecksum()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var note = new Note
        {
            Id = "1",
            ContentRtf = "{\\rtf1\\ansi\\deff0 {\\fonttbl {\\f0 Arial;}} Hello}",
            ContentText = "Hello",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        var payload = new BackupEnvelope
        {
            Notes = new List<Note> { note },
            Checksum = "deadbeef"
        };

        var importPath = Path.Combine(tempDir, "tampered.json");
        File.WriteAllText(importPath, JsonSerializer.Serialize(payload));

        var repository = new FakeNotesRepository();
        var service = new BackupService(repository, NullLogger<BackupService>.Instance, tempDir);

        var result = await service.ImportNotesAsync(importPath);

        Assert.False(result.ChecksumValid);
        Assert.True(result.ChecksumPresent);
        Assert.Equal(0, result.ImportedCount);
        Assert.NotEmpty(result.Failures);
        Assert.Empty(repository.Notes);
    }

    [Fact]
    public void ApplyRetentionPolicy_DeletesOldestBeyondThresholds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var oldBackup = Path.Combine(tempDir, "notes_2020-01-01.json");
        var recentBackup = Path.Combine(tempDir, "notes_2024-01-02.json");
        var newestBackup = Path.Combine(tempDir, "notes_2024-01-03.json");

        File.WriteAllText(oldBackup, "{}");
        File.WriteAllText(recentBackup, "{}");
        File.WriteAllText(newestBackup, "{}");

        File.SetLastWriteTime(oldBackup, DateTime.Now.AddDays(-10));
        File.SetLastWriteTime(recentBackup, DateTime.Now.AddDays(-1));
        File.SetLastWriteTime(newestBackup, DateTime.Now);

        var repository = new FakeNotesRepository();
        var service = new BackupService(repository, NullLogger<BackupService>.Instance, tempDir);
        service.ConfigureRetention(retentionDays: 5, retentionCount: 2);

        service.ApplyRetentionPolicy();

        Assert.False(File.Exists(oldBackup));
        var remaining = Directory.GetFiles(tempDir, "notes_*.json");
        Assert.Equal(2, remaining.Length);
        Assert.Contains(newestBackup, remaining);
        Assert.Contains(recentBackup, remaining);
    }

    [Fact]
    public async Task ListBackupsAsync_ReportsChecksumStatus()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var validNote = new Note
        {
            Id = "1",
            ContentRtf = "{\\rtf1\\ansi ",
            ContentText = "Hello",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        var payload = new BackupEnvelope
        {
            Notes = new List<Note> { validNote }
        };

        var checksumPayload = JsonSerializer.Serialize(payload.Notes, new JsonSerializerOptions { WriteIndented = false });
        payload.Checksum = ComputeChecksum(checksumPayload);

        var validPath = Path.Combine(tempDir, "notes_valid.json");
        await File.WriteAllTextAsync(validPath, JsonSerializer.Serialize(payload));

        var invalidPath = Path.Combine(tempDir, "notes_invalid.json");
        await File.WriteAllTextAsync(invalidPath, "not-json");

        var repository = new FakeNotesRepository();
        var service = new BackupService(repository, NullLogger<BackupService>.Instance, tempDir);

        var summaries = await service.ListBackupsAsync();

        var validSummary = summaries.First(s => s.FilePath == validPath);
        Assert.True(validSummary.ChecksumPresent);
        Assert.True(validSummary.ChecksumValid);
        Assert.Equal(1, validSummary.NoteCount);

        var invalidSummary = summaries.First(s => s.FilePath == invalidPath);
        Assert.False(invalidSummary.ChecksumValid);
    }

    private static string ComputeChecksum(string payload)
    {
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }

    private sealed class FakeNotesRepository : INotesRepository
    {
        private readonly List<Note> _notes;
        private AppSettings _settings;

        public FakeNotesRepository(List<Note>? notes = null, AppSettings? settings = null)
        {
            _notes = notes ?? new List<Note>();
            _settings = settings ?? new AppSettings();
        }

        public IReadOnlyList<Note> Notes => _notes;

        public Task<List<Note>> GetAllActiveNotesAsync()
        {
            return Task.FromResult(_notes.Where(n => !n.IsDeleted).ToList());
        }

        public Task<Note?> GetNoteByIdAsync(string id)
        {
            return Task.FromResult(_notes.FirstOrDefault(n => n.Id == id));
        }

        public Task UpsertNoteAsync(Note note)
        {
            var existing = _notes.FindIndex(n => n.Id == note.Id);
            if (existing >= 0)
            {
                _notes[existing] = note;
            }
            else
            {
                _notes.Add(note);
            }

            return Task.CompletedTask;
        }

        public Task SoftDeleteNoteAsync(string id)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note != null)
            {
                note.IsDeleted = true;
            }

            return Task.CompletedTask;
        }

        public Task<AppSettings> GetSettingsAsync()
        {
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(AppSettings settings)
        {
            _settings = settings;
            return Task.CompletedTask;
        }

        public Task<int> UpdateAllNoteFontsAsync(string fontFamily, double fontSize)
        {
            return Task.FromResult(0);
        }
    }
}
