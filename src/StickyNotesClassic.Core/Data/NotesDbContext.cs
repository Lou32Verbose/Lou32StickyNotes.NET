using Microsoft.Data.Sqlite;
using StickyNotesClassic.Core.Utilities;
using System.Collections.Generic;
using System.Threading;

namespace StickyNotesClassic.Core.Data;

/// <summary>
/// SQLite database context for sticky notes persistence.
/// Manages database initialization, migrations, and connection management.
/// </summary>
public class NotesDbContext : IDisposable
{
    private const string DatabaseFileName = "stickynotes.db";
    private const int CurrentSchemaVersion = 2;

    private readonly SqliteConnectionStringBuilder _connectionStringBuilder;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialized;

    public NotesDbContext(string? databasePath = null)
    {
        // Default to user's application data folder if not specified (cross-platform)
        var dbPath = databasePath ?? AppPathHelper.GetDatabaseFilePath(DatabaseFileName);

        _connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
    }

    public async Task<SqliteConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    /// <summary>
    /// Initializes the database schema if it doesn't exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var conn = await CreateConnectionAsync().ConfigureAwait(false);

            // Create schema version table
            await using var versionTableCmd = conn.CreateCommand();
            versionTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS schema_version (
                    version INTEGER PRIMARY KEY
                );";
            await versionTableCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Check current version
            await using var checkVersionCmd = conn.CreateCommand();
            checkVersionCmd.CommandText = "SELECT MAX(version) FROM schema_version;";
            var currentVersion = await checkVersionCmd.ExecuteScalarAsync().ConfigureAwait(false);
            var version = currentVersion == DBNull.Value || currentVersion == null ? 0 : Convert.ToInt32(currentVersion);

            // Run migrations
            if (version < 1)
            {
                await MigrateToVersion1Async(conn).ConfigureAwait(false);
                version = 1;
            }

            if (version < 2)
            {
                await MigrateToVersion2Async(conn).ConfigureAwait(false);
            }

            _initialized = true;
        }
        finally
        {
            _initGate.Release();
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

    private async Task MigrateToVersion2Async(SqliteConnection conn)
    {
        using var transaction = conn.BeginTransaction();

        try
        {
            var (fontFamily, fontSize) = await ReadFontDefaultsAsync(conn, transaction).ConfigureAwait(false);

            using var selectCmd = conn.CreateCommand();
            selectCmd.Transaction = transaction;
            selectCmd.CommandText = @"
                SELECT id, content_text, content_rtf
                FROM notes
                WHERE content_rtf NOT LIKE '{\\rtf%';";

            var updates = new List<(string Id, string ContentRtf)>();

            using (var reader = await selectCmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var id = reader.GetString(0);
                    var contentText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var rtf = RtfHelper.EnsureRtf(reader.GetString(2), contentText, fontFamily, fontSize);
                    updates.Add((id, rtf));
                }
            }

            foreach (var update in updates)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = @"UPDATE notes SET content_rtf = @rtf, updated_utc = @updated WHERE id = @id;";
                updateCmd.Parameters.AddWithValue("@rtf", update.ContentRtf);
                updateCmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("O"));
                updateCmd.Parameters.AddWithValue("@id", update.Id);

                await updateCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            using var updateVersionCmd = conn.CreateCommand();
            updateVersionCmd.Transaction = transaction;
            updateVersionCmd.CommandText = "INSERT INTO schema_version (version) VALUES (2);";
            await updateVersionCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<(string FontFamily, double FontSize)> ReadFontDefaultsAsync(SqliteConnection conn, SqliteTransaction transaction)
    {
        var fontFamily = "Segoe Print";
        var fontSize = 12.0;

        using var readSettings = conn.CreateCommand();
        readSettings.Transaction = transaction;
        readSettings.CommandText = "SELECT key, value FROM settings WHERE key IN ('DefaultFontFamily','DefaultFontSize');";

        try
        {
            using var reader = await readSettings.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var key = reader.GetString(0);
                var value = reader.GetString(1);

                if (key == "DefaultFontFamily" && !string.IsNullOrWhiteSpace(value))
                {
                    fontFamily = value;
                }
                else if (key == "DefaultFontSize" && double.TryParse(value, out var size))
                {
                    fontSize = size;
                }
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // settings table may not exist on older schema snapshots; fall back to defaults
            return (fontFamily, fontSize);
        }

        return (fontFamily, fontSize);
    }

    public void Dispose()
    {
        _initGate.Dispose();
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var walCmd = connection.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode=WAL;";
        await walCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var syncCmd = connection.CreateCommand();
        syncCmd.CommandText = "PRAGMA synchronous=NORMAL;";
        await syncCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var busyCmd = connection.CreateCommand();
        busyCmd.CommandText = "PRAGMA busy_timeout=5000;";
        await busyCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
