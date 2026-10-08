using System.Data;
using System.Data.Common;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Utilities;

/// <summary>
/// Opens every SQLite connection with a rollback journal that is emptied at commit rather than deleted.
/// </summary>
/// <remarks>
/// In SQLite's default journal mode every write transaction creates a <c>-journal</c> file next to the database and
/// deletes it at commit. A test that commits thousands of times churns thousands of new files through the temp
/// folder, and on Windows anything that opens a new file the moment it appears (an on-access scanner, an indexer)
/// can keep SQLite from deleting it: SQLite retries for well over a second, then fails the commit with "disk I/O
/// error". Truncating the journal instead leaves one file that is created once and never deleted, so no commit
/// depends on winning a delete against another process. The journal mode is a per-connection setting, so it is
/// applied as each connection opens.
/// </remarks>
public sealed class KeptJournalConnectionFactory : IConnectionFactory
{
    private readonly IConnectionFactory _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeptJournalConnectionFactory"/> class.
    /// </summary>
    /// <param name="inner">The SQLite connection factory being decorated.</param>
    public KeptJournalConnectionFactory(IConnectionFactory inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    /// <inheritdoc/>
    public Type DbConnectionType => _inner.DbConnectionType;

    /// <inheritdoc/>
    public DbConnection CreateConnection()
    {
        var connection = _inner.CreateConnection();
        connection.StateChange += OnStateChange;

        return connection;
    }

    private static void OnStateChange(object sender, StateChangeEventArgs e)
    {
        if (e.CurrentState != ConnectionState.Open || sender is not DbConnection connection)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=TRUNCATE;";
        command.ExecuteNonQuery();
    }
}
