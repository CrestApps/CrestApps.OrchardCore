using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Hands out SQLite connections that record the text of every statement executed through them. A query-plan
/// budget can only be asserted against a statement the gate can see, and the statements the document query
/// pipeline emits are built inside that pipeline rather than written anywhere in this repository: without a
/// recording connection the gate would have to hand-write an approximation, which proves a plan for a query
/// nothing runs.
/// </summary>
internal sealed class RecordingConnectionFactory : IConnectionFactory
{
    private readonly string _connectionString;
    private readonly List<string> _statements = [];
    private readonly List<RecordedExecution> _executions = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string handed to every created connection.</param>
    public RecordingConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    public Type DbConnectionType => typeof(SqliteConnection);

    /// <summary>
    /// Gets the text of every statement executed through a connection this factory created.
    /// </summary>
    public IReadOnlyList<string> Statements
    {
        get
        {
            lock (_gate)
            {
                return [.. _statements];
            }
        }
    }

    /// <summary>
    /// Gets the text and bound parameter values of every statement executed through a connection this factory
    /// created. A plan can only be measured for a statement whose parameters can be rebound, so a gate that
    /// captured the text alone would have to invent values the store never sent.
    /// </summary>
    public IReadOnlyList<RecordedExecution> Executions
    {
        get
        {
            lock (_gate)
            {
                return [.. _executions];
            }
        }
    }

    /// <inheritdoc/>
    public DbConnection CreateConnection()
        => new RecordingConnection(new SqliteConnection(_connectionString), Record);

    /// <summary>
    /// Forgets every statement recorded so far, so a subsequent assertion is about one measured operation
    /// rather than about everything the store has ever done.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _statements.Clear();
            _executions.Clear();
        }
    }

    private void Record(string commandText, DbParameterCollection parameters)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return;
        }

        var bound = new List<KeyValuePair<string, object>>(parameters.Count);

        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            bound.Add(new KeyValuePair<string, object>(parameter.ParameterName, parameter.Value));
        }

        lock (_gate)
        {
            _statements.Add(commandText);
            _executions.Add(new RecordedExecution(commandText, bound));
        }
    }

    /// <summary>
    /// One statement executed through a recording connection, with the parameter values it was sent with.
    /// </summary>
    /// <param name="CommandText">The statement text as the store built it.</param>
    /// <param name="Parameters">The parameter names and values bound when it ran.</param>
    internal sealed record RecordedExecution(string CommandText, IReadOnlyList<KeyValuePair<string, object>> Parameters);

    private sealed class RecordingConnection : DbConnection
    {
        private readonly SqliteConnection _inner;
        private readonly Action<string, DbParameterCollection> _record;

        public RecordingConnection(SqliteConnection inner, Action<string, DbParameterCollection> record)
        {
            _inner = inner;
            _record = record;
        }

        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }

        public override string Database => _inner.Database;

        public override string DataSource => _inner.DataSource;

        public override string ServerVersion => _inner.ServerVersion;

        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public override void Close() => _inner.Close();

        public override void Open() => _inner.Open();

        // The transaction has to be wrapped as well. A caller that begins a transaction and then works through
        // transaction.Connection would otherwise reach the unwrapped connection and every statement it issues
        // would go unrecorded, which reads as a passing budget rather than as a blind gate.
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new RecordingTransaction(_inner.BeginTransaction(isolationLevel), this);

        protected override DbCommand CreateDbCommand()
            => new RecordingCommand(_inner.CreateCommand(), this, _record);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class RecordingCommand : DbCommand
    {
        private readonly SqliteCommand _inner;
        private readonly DbConnection _owner;
        private readonly Action<string, DbParameterCollection> _record;

        public RecordingCommand(SqliteCommand inner, DbConnection owner, Action<string, DbParameterCollection> record)
        {
            _inner = inner;
            _owner = owner;
            _record = record;
        }

        public override string CommandText
        {
            get => _inner.CommandText;
            set => _inner.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _inner.CommandTimeout;
            set => _inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _inner.CommandType;
            set => _inner.CommandType = value;
        }

        public override bool DesignTimeVisible
        {
            get => _inner.DesignTimeVisible;
            set => _inner.DesignTimeVisible = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _inner.UpdatedRowSource;
            set => _inner.UpdatedRowSource = value;
        }

        protected override DbConnection DbConnection
        {
            get => _owner;
            set { }
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

        protected override DbTransaction DbTransaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = value switch
            {
                RecordingTransaction recording => recording.Inner,
                SqliteTransaction sqlite => sqlite,
                _ => null,
            };
        }

        public override void Cancel() => _inner.Cancel();

        public override int ExecuteNonQuery()
        {
            _record(_inner.CommandText, _inner.Parameters);

            return _inner.ExecuteNonQuery();
        }

        public override object ExecuteScalar()
        {
            _record(_inner.CommandText, _inner.Parameters);

            return _inner.ExecuteScalar();
        }

        public override void Prepare() => _inner.Prepare();

        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            _record(_inner.CommandText, _inner.Parameters);

            return _inner.ExecuteReader(behavior);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class RecordingTransaction : DbTransaction
    {
        private readonly DbConnection _owner;

        public RecordingTransaction(SqliteTransaction inner, DbConnection owner)
        {
            Inner = inner;
            _owner = owner;
        }

        public SqliteTransaction Inner { get; }

        public override IsolationLevel IsolationLevel => Inner.IsolationLevel;

        protected override DbConnection DbConnection => _owner;

        public override void Commit() => Inner.Commit();

        public override void Rollback() => Inner.Rollback();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
