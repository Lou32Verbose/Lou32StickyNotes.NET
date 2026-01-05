using Microsoft.Extensions.Logging;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;
using StickyNotesClassic.Core.Services;
using StickyNotesClassic.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace StickyNotesClassic.App.Services;

/// <summary>
/// Service for backup, import, and export of notes.
/// </summary>
public class BackupService : IDisposable
{
    private readonly INotesRepository _repository;
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupDirectory;
    private readonly string _logsDirectory;
    private readonly JsonSerializerOptions _payloadOptions = new()
    {
        WriteIndented = false
    };
    private Timer? _dailyBackupTimer;
    private int _retentionDays = 7;
    private int _retentionCount = 10;

    public BackupService(INotesRepository repository, ILogger<BackupService> logger, string? backupDirectory = null)
    {
        _repository = repository;
        _logger = logger;

        // Default backup location: app data/Backups (cross-platform)
        _backupDirectory = backupDirectory ?? AppPathHelper.GetBackupsDirectory();
        _logsDirectory = AppPathHelper.GetLogsDirectory();

        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// Configures retention controls for automatic backups.
    /// </summary>
    public void ConfigureRetention(int retentionDays, int retentionCount)
    {
        _retentionDays = Math.Clamp(retentionDays, 1, 365);
        _retentionCount = Math.Max(1, retentionCount);
    }

    /// <summary>
    /// Exports all active notes to a JSON file.
    /// </summary>
    public async Task<BackupExportResult> ExportNotesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _backupDirectory);

        cancellationToken.ThrowIfCancellationRequested();
        var notes = await _repository.GetAllActiveNotesAsync();

        cancellationToken.ThrowIfCancellationRequested();
        var notesJson = JsonSerializer.Serialize(notes, _payloadOptions);
        var checksum = ComputeChecksum(notesJson);
        var payload = new BackupEnvelope
        {
            Notes = notes,
            Checksum = checksum
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        // Apply retention after successful export
        ApplyRetentionPolicy();

        return new BackupExportResult
        {
            FilePath = filePath,
            ExportedCount = notes.Count,
            Checksum = checksum
        };
    }

    /// <summary>
    /// Imports notes from a JSON file.
    /// Conflict resolution: assigns new IDs to avoid conflicts.
    /// </summary>
    public async Task<BackupImportResult> ImportNotesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Import file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var importEnvelope = DeserializeEnvelope(json);

        var importedNotes = importEnvelope.Notes;

        var result = new BackupImportResult();
        result.FilePath = filePath;
        result.LogDirectory = _logsDirectory;
        result.ChecksumPresent = importEnvelope.ChecksumPresent;
        result.ChecksumValid = importEnvelope.ChecksumValid;

        if (importedNotes == null || importedNotes.Count == 0)
        {
            return result;
        }

        if (importEnvelope.ChecksumPresent && !importEnvelope.ChecksumValid)
        {
            result.Failures.Add(new ImportFailure
            {
                Reason = "Backup checksum mismatch; import aborted to protect data integrity."
            });
            return result;
        }

        foreach (var note in importedNotes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Generate new ID to avoid conflicts
            note.Id = Guid.NewGuid().ToString();
            
            // Reset deleted flag (we're importing active notes)
            note.IsDeleted = false;

            // Update timestamps
            note.CreatedUtc = DateTime.UtcNow;
            note.UpdatedUtc = DateTime.UtcNow;

            // Validate RTF content to avoid corrupt data imports
            var validation = ValidationService.ValidateRtfContent(note.ContentRtf);
            if (!validation.IsValid)
            {
                var failure = new ImportFailure
                {
                    Reason = validation.ErrorMessage ?? "Invalid content",
                    OriginalContentPreview = note.ContentText
                };

                _logger.LogWarning("Skipping imported note due to invalid RTF: {Reason}", failure.Reason);
                result.Failures.Add(failure);
                continue;
            }

            // Insert into database
            try
            {
                await _repository.UpsertNoteAsync(note);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                var failure = new ImportFailure
                {
                    Reason = ex.Message,
                    OriginalContentPreview = note.ContentText
                };

                _logger.LogError(ex, "Failed to import note");
                result.Failures.Add(failure);
            }
        }

        result.FilePath = filePath;
        return result;
    }

    /// <summary>
    /// Schedules daily automatic backups.
    /// </summary>
    public void ScheduleDailyBackup(int retentionDays, int retentionCount)
    {
        ConfigureRetention(retentionDays, retentionCount);

        // Calculate time until next 3 AM
        var now = DateTime.Now;
        var next3AM = DateTime.Today.AddDays(1).AddHours(3);
        if (now.Hour < 3)
        {
            next3AM = DateTime.Today.AddHours(3);
        }

        var timeUntilNext3AM = next3AM - now;

        // Set up timer to run daily
        _dailyBackupTimer?.Dispose();
        _dailyBackupTimer = new Timer(24 * 60 * 60 * 1000); // 24 hours
        _dailyBackupTimer.Elapsed += async (s, e) => await PerformDailyBackupAsync();
        _dailyBackupTimer.AutoReset = true;

        // Do initial backup after the calculated delay
        Task.Delay(timeUntilNext3AM).ContinueWith(async _ =>
        {
            await PerformDailyBackupAsync();
            _dailyBackupTimer.Start();
        });
    }

    /// <summary>
    /// Performs a daily backup and cleans up old backups.
    /// </summary>
    public async Task PerformDailyBackupAsync()
    {
        try
        {
            // Create backup filename with today's date
            var backupFileName = $"notes_{DateTime.Now:yyyy-MM-dd}.json";
            var backupPath = Path.Combine(_backupDirectory, backupFileName);

            // Export notes
            await ExportNotesAsync(backupPath);
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            _logger.LogError(ex, "Auto-backup failed");
        }
    }

    /// <summary>
    /// Lists backup files with checksum status for restore UX.
    /// </summary>
    public async Task<IReadOnlyList<BackupSummary>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return Array.Empty<BackupSummary>();
        }

        var files = Directory.GetFiles(_backupDirectory, "*.json");
        var summaries = new List<BackupSummary>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new FileInfo(file);
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var envelope = DeserializeEnvelope(json);

                summaries.Add(new BackupSummary
                {
                    FilePath = file,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    SizeBytes = info.Length,
                    NoteCount = envelope.Notes?.Count ?? 0,
                    ChecksumPresent = envelope.ChecksumPresent,
                    ChecksumValid = envelope.ChecksumValid
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect backup {Path}", file);
                summaries.Add(new BackupSummary
                {
                    FilePath = file,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    SizeBytes = info.Length,
                    NoteCount = 0,
                    ChecksumPresent = false,
                    ChecksumValid = false
                });
            }
        }

        return summaries
            .OrderByDescending(b => b.LastWriteTimeUtc)
            .ToList();
    }

    /// <summary>
    /// Removes backup files beyond retention thresholds.
    /// </summary>
    public void ApplyRetentionPolicy()
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
            var backupFiles = Directory
                .GetFiles(_backupDirectory, "notes_*.json")
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .ToList();

            for (var i = 0; i < backupFiles.Count; i++)
            {
                var fileInfo = backupFiles[i];
                if (fileInfo.LastWriteTime < cutoffDate || i >= _retentionCount)
                {
                    File.Delete(fileInfo.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup cleanup failed");
        }
    }

    /// <summary>
    /// Gets the backup directory path.
    /// </summary>
    public string GetBackupDirectory() => _backupDirectory;

    public void Dispose()
    {
        _dailyBackupTimer?.Dispose();
        _dailyBackupTimer = null;
    }

    private string ComputeChecksum(string payload)
    {
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }

    private DeserializedEnvelope DeserializeEnvelope(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<BackupEnvelope>(json);
            if (envelope?.Notes != null)
            {
                var checksumPresent = envelope.ChecksumPresent;
                var checksumValid = true;

                if (checksumPresent)
                {
                    var expected = ComputeChecksum(JsonSerializer.Serialize(envelope.Notes, _payloadOptions));
                    checksumValid = string.Equals(expected, envelope.Checksum, StringComparison.OrdinalIgnoreCase);
                }

                return new DeserializedEnvelope
                {
                    Notes = envelope.Notes,
                    ChecksumPresent = checksumPresent,
                    ChecksumValid = checksumValid
                };
            }
        }
        catch
        {
            // Fall through to legacy format handling
        }

        try
        {
            var notes = JsonSerializer.Deserialize<List<Note>>(json, _payloadOptions);
            return new DeserializedEnvelope
            {
                Notes = notes,
                ChecksumPresent = false,
                ChecksumValid = true
            };
        }
        catch
        {
            return new DeserializedEnvelope
            {
                Notes = new List<Note>()
            };
        }
    }
}

public sealed class BackupExportResult
{
    public string FilePath { get; init; } = string.Empty;

    public int ExportedCount { get; init; }

    public string Checksum { get; init; } = string.Empty;
}

public sealed class BackupImportResult
{
    public string FilePath { get; set; } = string.Empty;

    public int ImportedCount { get; set; }

    public string LogDirectory { get; set; } = string.Empty;

    public List<ImportFailure> Failures { get; } = new();

    public bool ChecksumPresent { get; set; }

    public bool ChecksumValid { get; set; } = true;

    public int SkippedCount => Failures.Count;
}

public sealed class ImportFailure
{
    public string Reason { get; set; } = string.Empty;

    public string? OriginalContentPreview { get; set; }
}

public sealed class BackupEnvelope
{
    public List<Note>? Notes { get; set; }

    public string? Checksum { get; set; }

    public bool ChecksumPresent => !string.IsNullOrWhiteSpace(Checksum);
}

internal sealed class DeserializedEnvelope
{
    public List<Note>? Notes { get; init; }

    public bool ChecksumPresent { get; init; }

    public bool ChecksumValid { get; init; }
}

public sealed class BackupSummary
{
    public string FilePath { get; init; } = string.Empty;

    public DateTime LastWriteTimeUtc { get; init; }

    public long SizeBytes { get; init; }

    public int NoteCount { get; init; }

    public bool ChecksumPresent { get; init; }

    public bool ChecksumValid { get; init; }
}
