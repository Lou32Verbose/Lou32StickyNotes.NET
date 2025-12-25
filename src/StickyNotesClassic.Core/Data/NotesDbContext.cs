using Microsoft.Data.Sqlite;

namespace StickyNotesClassic.Core.Data;

/// <summary>
/// SQLite database context for sticky notes persistence.
/// Manages database initialization, migrations, and connection management.
/// </summary>
public class NotesDbContext : IDisposable
{
    private const string DatabaseFileName = "stickynotes.db";
    private const int CurrentSchemaVersion = 1;

    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public NotesDbContext(string? databasePath = null)
    {
        // Default to user's AppData folder if not specified
        var dbPath = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lou32StickyNotes",
            DatabaseFileName);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath};";
    }

    /// <summary>
    /// Gets or creates a database connection.
    /// </summary>
    public SqliteConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            // Enable WAL mode for better concurrency and crash safety
            using var walCmd = _connection.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();

            // Set synchronous=NORMAL for good performance with acceptable safety
            using var syncCmd = _connection.CreateCommand();
            syncCmd.CommandText = "PRAGMA synchronous=NORMAL;";
            syncCmd.ExecuteNonQuery();
        }

        return _connection;
    }

    /// <summary>
    /// Initializes the database schema if it doesn't exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        var conn = GetConnection();

        // Create schema version table
        using var versionTableCmd = conn.CreateCommand();
        versionTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY
            );";
        await versionTableCmd.ExecuteNonQueryAsync();

        // Check current version
        using var checkVersionCmd = conn.CreateCommand();
        checkVersionCmd.CommandText = "SELECT MAX(version) FROM schema_version;";
        var currentVersion = await checkVersionCmd.ExecuteScalarAsync();
        var version = currentVersion == DBNull.Value || currentVersion == null ? 0 : Convert.ToInt32(currentVersion);

        // Run migrations
        if (version < CurrentSchemaVersion)
        {
            await MigrateToVersion1Async(conn);
        }
    }

    private async Task MigrateToVersion1Async(SqliteConnection conn)
    {
        using var transaction = conn.BeginTransaction();

        try
        {
            // Create notes table
            using var createNotesCmd = conn.CreateCommand();
            createNotesCmd.Transaction = transaction;
            createNotesCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS notes (
                    id            TEXT PRIMARY KEY,
                    created_utc   TEXT NOT NULL,
                    updated_utc   TEXT NOT NULL,
                    
                    color         INTEGER NOT NULL,
                    is_topmost    INTEGER NOT NULL DEFAULT 0,
                    
                    x             REAL NOT NULL,
                    y             REAL NOT NULL,
                    width         REAL NOT NULL,
                    height        REAL NOT NULL,
                    
                    content_rtf   TEXT NOT NULL,
                    content_text  TEXT NOT NULL,
                    
                    is_deleted    INTEGER NOT NULL DEFAULT 0
                );";
            await createNotesCmd.ExecuteNonQueryAsync();

            // Create indices
            using var createUpdateIdxCmd = conn.CreateCommand();
            createUpdateIdxCmd.Transaction = transaction;
            createUpdateIdxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_notes_updated ON notes(updated_utc);";
            await createUpdateIdxCmd.ExecuteNonQueryAsync();

            using var createDeletedIdxCmd = conn.CreateCommand();
            createDeletedIdxCmd.Transaction = transaction;
            createDeletedIdxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_notes_deleted ON notes(is_deleted);";
            await createDeletedIdxCmd.ExecuteNonQueryAsync();

            // Create settings table
            using var createSettingsCmd = conn.CreateCommand();
            createSettingsCmd.Transaction = transaction;
            createSettingsCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS settings (
                    key   TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );";
            await createSettingsCmd.ExecuteNonQueryAsync();

            // Update schema version
            using var updateVersionCmd = conn.CreateCommand();
            updateVersionCmd.Transaction = transaction;
            updateVersionCmd.CommandText = "INSERT INTO schema_version (version) VALUES (1);";
            await updateVersionCmd.ExecuteNonQueryAsync();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
