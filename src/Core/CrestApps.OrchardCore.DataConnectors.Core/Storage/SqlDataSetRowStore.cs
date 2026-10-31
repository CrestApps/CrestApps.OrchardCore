using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json.Nodes;
using OrchardCore.Data;
using YesSql;

namespace CrestApps.OrchardCore.DataConnectors.Storage;

/// <summary>
/// Stores the rows of imported data sets in one table of the tenant's database, with the tenant's table prefix and
/// schema, through the dialect YesSql uses for the tenant.
/// </summary>
/// <remarks>
/// Each call opens its own connection and commits its own transaction, so a long import never holds a transaction
/// of the request's session.
/// </remarks>
internal sealed class SqlDataSetRowStore : IDataSetRowStore
{
    private const int _rowsPerInsert = 100;
    private const int _keysPerStatement = 500;
    private const int _longCommandTimeoutSeconds = 600;

    private readonly Func<DbConnection> _createConnection;
    private readonly ISqlDialect _dialect;
    private readonly string _table;
    private readonly string _id;
    private readonly string _dataSetId;
    private readonly string _version;
    private readonly string _rowKey;
    private readonly string _data;

    public SqlDataSetRowStore(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor)
        : this(dbConnectionAccessor.CreateConnection, store.Configuration.SqlDialect, store.Configuration.TablePrefix, store.Configuration.Schema)
    {
    }

    internal SqlDataSetRowStore(
        Func<DbConnection> createConnection,
        ISqlDialect dialect,
        string tablePrefix,
        string schema)
    {
        _createConnection = createConnection;
        _dialect = dialect;
        _table = dialect.QuoteForTableName((tablePrefix ?? string.Empty) + DataConnectorConstants.RowsTableName, schema);
        _id = dialect.QuoteForColumnName("Id");
        _dataSetId = dialect.QuoteForColumnName("DataSetId");
        _version = dialect.QuoteForColumnName("Version");
        _rowKey = dialect.QuoteForColumnName("RowKey");
        _data = dialect.QuoteForColumnName("Data");
    }

    public async Task WriteAsync(string dataSetId, string version, IReadOnlyList<DataSetRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetId);
        ArgumentException.ThrowIfNullOrEmpty(version);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        for (var start = 0; start < rows.Count; start += _rowsPerInsert)
        {
            var count = Math.Min(_rowsPerInsert, rows.Count - start);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;

            var sql = new StringBuilder($"INSERT INTO {_table} ({_dataSetId}, {_version}, {_rowKey}, {_data}) VALUES ");

            AddParameter(command, "d", dataSetId);
            AddParameter(command, "v", version);

            for (var index = 0; index < count; index++)
            {
                var row = rows[start + index];

                if (index > 0)
                {
                    sql.Append(", ");
                }

                sql.Append($"(@d, @v, @k{index}, @j{index})");
                AddParameter(command, "k" + index, row.Key);
                AddParameter(command, "j" + index, (row.Data ?? []).ToJsonString());
            }

            command.CommandText = sql.ToString();

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> CopyAsync(string dataSetId, string fromVersion, string toVersion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetId);
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        ArgumentException.ThrowIfNullOrEmpty(toVersion);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandTimeout = _longCommandTimeoutSeconds;
        command.CommandText =
            $"INSERT INTO {_table} ({_dataSetId}, {_version}, {_rowKey}, {_data}) " +
            $"SELECT {_dataSetId}, @to, {_rowKey}, {_data} FROM {_table} WHERE {_dataSetId} = @d AND {_version} = @from ORDER BY {_id}";
        AddParameter(command, "d", dataSetId);
        AddParameter(command, "from", fromVersion);
        AddParameter(command, "to", toVersion);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> DeleteKeysAsync(string dataSetId, string version, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetId);
        ArgumentException.ThrowIfNullOrEmpty(version);
        ArgumentNullException.ThrowIfNull(keys);

        var distinct = keys.Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal).ToArray();

        if (distinct.Length == 0)
        {
            return 0;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        long deleted = 0;

        foreach (var chunk in distinct.Chunk(_keysPerStatement))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {_table} WHERE {_dataSetId} = @d AND {_version} = @v AND {_rowKey} IN ({InList(command, "k", chunk)})";
            AddParameter(command, "d", dataSetId);
            AddParameter(command, "v", version);

            deleted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return deleted;
    }

    public async Task<IReadOnlyList<StoredDataSetRow>> ReadAsync(
        string dataSetId,
        string version,
        long afterId,
        int take,
        IReadOnlyCollection<string> keys = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetId);
        ArgumentException.ThrowIfNullOrEmpty(version);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        if (keys is { Count: 0 })
        {
            return [];
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var where = $"{_dataSetId} = @d AND {_version} = @v AND {_id} > @after";

        if (keys is not null)
        {
            where += $" AND {_rowKey} IN ({InList(command, "k", keys.Distinct(StringComparer.Ordinal).Take(_keysPerStatement).ToArray())})";
        }

        command.CommandText = Limit($"{_id}, {_rowKey}, {_data}", where, $"{_id}", take);
        AddParameter(command, "d", dataSetId);
        AddParameter(command, "v", version);
        AddParameter(command, "after", afterId);

        var rows = new List<StoredDataSetRow>(Math.Min(take, 1_000));

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Convert.ToInt64(reader.GetValue(0));
            var key = await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetString(1);
            var json = await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2);

            rows.Add(new StoredDataSetRow(id, key, string.IsNullOrEmpty(json) ? [] : JsonNode.Parse(json) as JsonObject ?? []));
        }

        return rows;
    }

    public async Task<long> CountAsync(string dataSetId, string version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetId);
        ArgumentException.ThrowIfNullOrEmpty(version);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandTimeout = _longCommandTimeoutSeconds;
        command.CommandText = $"SELECT COUNT(*) FROM {_table} WHERE {_dataSetId} = @d AND {_version} = @v";
        AddParameter(command, "d", dataSetId);
        AddParameter(command, "v", version);

        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task DeleteVersionAsync(string dataSetId, string version, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetId);
        ArgumentException.ThrowIfNullOrEmpty(version);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandTimeout = _longCommandTimeoutSeconds;
        command.CommandText = $"DELETE FROM {_table} WHERE {_dataSetId} = @d AND {_version} = @v";
        AddParameter(command, "d", dataSetId);
        AddParameter(command, "v", version);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(string dataSetId, IReadOnlyCollection<string> keepVersions = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSetId);

        var keep = (keepVersions ?? []).Where(version => !string.IsNullOrEmpty(version)).Distinct(StringComparer.Ordinal).ToArray();

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandTimeout = _longCommandTimeoutSeconds;
        command.CommandText = keep.Length == 0
            ? $"DELETE FROM {_table} WHERE {_dataSetId} = @d"
            : $"DELETE FROM {_table} WHERE {_dataSetId} = @d AND {_version} NOT IN ({InList(command, "keep", keep)})";
        AddParameter(command, "d", dataSetId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = _createConnection();

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }

        return connection;
    }

    private string Limit(string columns, string where, string orderBy, int take)
    {
        // SQL Server has no LIMIT clause; SQLite, PostgreSQL and MySQL have no TOP.
        return string.Equals(_dialect.Name, "SqlServer", StringComparison.OrdinalIgnoreCase)
            ? $"SELECT TOP ({take}) {columns} FROM {_table} WHERE {where} ORDER BY {orderBy}"
            : $"SELECT {columns} FROM {_table} WHERE {where} ORDER BY {orderBy} LIMIT {take}";
    }

    private static string InList(DbCommand command, string prefix, string[] values)
    {
        var names = new string[values.Length];

        for (var index = 0; index < values.Length; index++)
        {
            names[index] = "@" + prefix + index;
            AddParameter(command, prefix + index, values[index]);
        }

        return string.Join(", ", names);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@" + name;
        parameter.Value = value ?? DBNull.Value;

        if (value is string or null)
        {
            parameter.DbType = DbType.String;
        }

        command.Parameters.Add(parameter);
    }
}
