using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace StickyNotesClassic.App.Services;

/// <summary>
/// Service for backup, import, and export of notes.
/// </summary>
public class BackupService : IDisposable
{
    private readonly INotesRepository _repository;
    private readonly string _backupDirectory;
    private Timer? _dailyBackupTimer;
    private int _retentionDays = 7;

    public BackupService(INotesRepository repository)
    {
        _repository = repository;
        
        // Default backup location: %LocalAppData%/Lou32StickyNotes/Backups
        _backupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lou32StickyNotes",
            "Backups");

        // Ensure backup directory exists
        if (!Directory.Exists(_backupDirectory))
        {
            Directory.CreateDirectory(_backupDirectory);
        }
    }

    /// <summary>
    /// Exports all active notes to a JSON file.
    /// </summary>
    public async Task ExportNotesAsync(string filePath)
    {
        var notes = await _repository.GetAllActiveNotesAsync();
        
        var json = JsonSerializer.Serialize(notes, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Imports notes from a JSON file.
    /// Conflict resolution: assigns new IDs to avoid conflicts.
    /// </summary>
    public async Task<int> ImportNotesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Import file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath);
        var importedNotes = JsonSerializer.Deserialize<List<Note>>(json);

        if (importedNotes == null || importedNotes.Count == 0)
        {
            return 0;
        }

        int importedCount = 0;

        foreach (var note in importedNotes)
        {
            // Generate new ID to avoid conflicts
            note.Id = Guid.NewGuid().ToString();
            
            // Reset deleted flag (we're importing active notes)
            note.IsDeleted = false;
            
            // Update timestamps
            note.CreatedUtc = DateTime.UtcNow;
            note.UpdatedUtc = DateTime.UtcNow;
            
            // Insert into database
            await _repository.UpsertNoteAsync(note);
            importedCount++;
        }

        return importedCount;
    }

    /// <summary>
    /// Schedules daily automatic backups.
    /// </summary>
    public void ScheduleDailyBackup(int retentionDays)
    {
        _retentionDays = retentionDays;

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

            // Cleanup old backups
            CleanupOldBackups();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            Console.WriteLine($"Auto-backup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes backup files older than retention period.
    /// </summary>
    private void CleanupOldBackups()
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
            var backupFiles = Directory.GetFiles(_backupDirectory, "notes_*.json");

            foreach (var file in backupFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Backup cleanup failed: {ex.Message}");
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
}
