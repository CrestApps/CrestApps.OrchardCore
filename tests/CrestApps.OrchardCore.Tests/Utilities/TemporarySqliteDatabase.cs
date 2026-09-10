using Microsoft.Data.Sqlite;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Utilities;

/// <summary>
/// Releases a temporary SQLite database created by a test.
/// </summary>
public static class TemporarySqliteDatabase
{
    private const int DeleteAttempts = 20;

    private static readonly TimeSpan _deleteRetryDelay = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// Disposes the store and removes its temporary database file.
    /// </summary>
    /// <remarks>
    /// Disposing the store does not guarantee the operating system has released the file. On Windows a
    /// delete issued immediately afterwards can still fail with a sharing violation, because the SQLite
    /// handle is closed asynchronously and any connection returned to a pool stays open. Pools are cleared
    /// first, then the delete is retried briefly. A file that still cannot be removed is left in the
    /// temporary directory rather than failing the test, since the database is disposable scratch state and
    /// its removal is not what the test is asserting.
    /// </remarks>
    /// <param name="store">The store to dispose. Ignored when <see langword="null"/>.</param>
    /// <param name="databasePath">The full path of the temporary database file to delete.</param>
    public static void DisposeAndDelete(IStore store, string databasePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(databasePath);

        store?.Dispose();
        SqliteConnection.ClearAllPools();
        Delete(databasePath);
    }

    /// <summary>
    /// Removes a temporary database file, retrying while the operating system still holds it open.
    /// </summary>
    /// <param name="databasePath">The full path of the temporary database file to delete.</param>
    public static void Delete(string databasePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(databasePath);

        for (var attempt = 0; attempt < DeleteAttempts; attempt++)
        {
            try
            {
                File.Delete(databasePath);

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(_deleteRetryDelay);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(_deleteRetryDelay);
            }
        }
    }
}
