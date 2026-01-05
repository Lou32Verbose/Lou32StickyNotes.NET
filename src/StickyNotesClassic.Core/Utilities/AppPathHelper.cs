using System;
using System.IO;

namespace StickyNotesClassic.Core.Utilities;

/// <summary>
/// Provides cross-platform application paths for data, logs, and backups.
/// </summary>
public static class AppPathHelper
{
    private const string AppFolderName = "Lou32StickyNotes";

    /// <summary>
    /// Gets the base application data directory, falling back to common user locations when necessary.
    /// Ensures the directory exists.
    /// </summary>
    public static string GetBaseAppDataDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = Path.GetTempPath();
        }

        var appPath = Path.Combine(basePath, AppFolderName);
        Directory.CreateDirectory(appPath);
        return appPath;
    }

    /// <summary>
    /// Gets the backups directory path and ensures it exists.
    /// </summary>
    public static string GetBackupsDirectory()
    {
        var backupsPath = Path.Combine(GetBaseAppDataDirectory(), "Backups");
        Directory.CreateDirectory(backupsPath);
        return backupsPath;
    }

    /// <summary>
    /// Gets the logs directory path and ensures it exists.
    /// </summary>
    public static string GetLogsDirectory()
    {
        var logsPath = Path.Combine(GetBaseAppDataDirectory(), "Logs");
        Directory.CreateDirectory(logsPath);
        return logsPath;
    }

    /// <summary>
    /// Gets the database file path, ensuring the containing directory exists.
    /// </summary>
    public static string GetDatabaseFilePath(string fileName)
    {
        var dbPath = Path.Combine(GetBaseAppDataDirectory(), fileName);
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return dbPath;
    }
}
