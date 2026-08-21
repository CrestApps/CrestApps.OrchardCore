using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Cms.Web;

/// <summary>
/// Tunes each tenant's SQLite database for the concurrent write load this application produces (real-time voice
/// webhooks, the Contact Center event outbox, and periodic background sweeps all write independently). By default
/// YesSql's SQLite provider opens connections in rollback-journal mode with no busy timeout, so the moment two
/// writers overlap SQLite fails immediately with "database is locked" instead of waiting. This wraps the tenant's
/// connection factory so every SQLite connection enables:
/// <list type="bullet">
/// <item>WAL journalling, so readers and a single writer no longer block each other;</item>
/// <item>a busy timeout, so a second writer waits for the lock (up to the timeout) instead of erroring;</item>
/// <item><c>synchronous=NORMAL</c>, safe under WAL and markedly faster than the FULL default.</item>
/// </list>
/// It is a no-op for non-SQLite providers.
/// </summary>
internal sealed class SqliteConnectionTuningTenantEvents : ModularTenantEvents
{
    private readonly IStore _store;
    private readonly ILogger _logger;

    public SqliteConnectionTuningTenantEvents(IStore store, ILogger<SqliteConnectionTuningTenantEvents> logger)
    {
        _store = store;
        _logger = logger;
    }

    public override Task ActivatingAsync()
    {
        var configuration = _store.Configuration;
        var factory = configuration.ConnectionFactory;

        // Only wrap a SQLite factory, and never wrap twice (tenant activation can run more than once).
        if (factory is not null &&
            factory is not SqlitePragmaConnectionFactory &&
            string.Equals(factory.DbConnectionType?.Name, "SqliteConnection", StringComparison.Ordinal))
        {
            configuration.ConnectionFactory = new SqlitePragmaConnectionFactory(factory, _logger);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// A YesSql connection factory decorator that runs the SQLite tuning pragmas on every connection as it opens.
/// </summary>
internal sealed class SqlitePragmaConnectionFactory : IConnectionFactory
{
    // Run each pragma as its own statement. busy_timeout is set FIRST -- and separately -- so it is guaranteed to
    // apply (a single multi-statement command whose first statement, journal_mode, returns a result row does not
    // reliably execute the later statements), and so the journal_mode change itself waits for the lock rather than
    // failing when another connection is mid-write.
    private static readonly string[] TuningPragmas =
    [
        "PRAGMA busy_timeout=10000;",
        "PRAGMA journal_mode=WAL;",
        "PRAGMA synchronous=NORMAL;",
    ];

    private readonly IConnectionFactory _inner;
    private readonly ILogger _logger;

    // The tuning is verified once per process: the first connection to open reads the pragmas back and logs them,
    // so a run's logs prove whether the busy timeout and WAL mode actually took effect (a per-connection PRAGMA that
    // silently fails to apply is otherwise invisible until it manifests as a "database is locked" under load).
    private static int _verified;

    public SqlitePragmaConnectionFactory(IConnectionFactory inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public Type DbConnectionType => _inner.DbConnectionType;

    public DbConnection CreateConnection()
    {
        var connection = _inner.CreateConnection();
        connection.StateChange += OnStateChange;

        return connection;
    }

    private void OnStateChange(object sender, StateChangeEventArgs e)
    {
        if (e.CurrentState != ConnectionState.Open || sender is not DbConnection connection)
        {
            return;
        }

        foreach (var pragma in TuningPragmas)
        {
            using var command = connection.CreateCommand();
            command.CommandText = pragma;
            command.ExecuteNonQuery();
        }

        if (Interlocked.Exchange(ref _verified, 1) == 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "SQLite connection tuning applied: busy_timeout={BusyTimeout}, journal_mode={JournalMode}, synchronous={Synchronous}.",
                ReadScalar(connection, "PRAGMA busy_timeout;"),
                ReadScalar(connection, "PRAGMA journal_mode;"),
                ReadScalar(connection, "PRAGMA synchronous;"));
        }
    }

    private static string ReadScalar(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return command.ExecuteScalar()?.ToString() ?? "<null>";
    }
}
