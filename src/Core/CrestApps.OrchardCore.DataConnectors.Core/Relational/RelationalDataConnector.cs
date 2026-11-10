using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using CrestApps.Core;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors.Relational;

/// <summary>
/// How a data set reads a relational database: a table or view (the data set's resource), or a query of its own.
/// </summary>
public sealed class RelationalDataSetSettings
{
    /// <summary>
    /// Gets or sets a single <c>SELECT</c> query read instead of the resource, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The query runs as a derived table, <c>SELECT … FROM (query) AS q</c>, inside a transaction that is always rolled
    /// back, so it must be one query expression: no <c>ORDER BY</c> without a row limit, no common table expression and
    /// no second statement. Parameters of the read are bound by name, such as <c>@Since</c>.
    /// </remarks>
    public string Query { get; set; }

    /// <summary>
    /// Gets or sets the seconds a query may run.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// The shared logic of connectors for relational databases: discovery of tables and views, fields from the result
/// set's schema, streamed reads, and conditions applied by the database. A connector supplies the dialect: how to
/// connect, quote and limit, and how its catalog lists tables.
/// </summary>
/// <remarks>
/// Identifiers are always quoted, values are always bound as parameters, and every read runs in a transaction that is
/// rolled back. Give the connection a login that can only read what the data sets need: the connector can't take away
/// rights the login has.
/// </remarks>
public abstract class RelationalDataConnector
{
    private const string _derivedTableAlias = "q";

    /// <summary>
    /// Opens a connection to the database.
    /// </summary>
    /// <param name="context">The connection's settings and secrets.</param>
    /// <param name="cancellationToken">The token that cancels the opening.</param>
    /// <returns>The open connection.</returns>
    protected abstract Task<DbConnection> OpenConnectionAsync(DataConnectorContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Quotes one identifier, such as a table, schema or column name, escaping what it contains.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The quoted identifier.</returns>
    protected abstract string QuoteIdentifier(string identifier);

    /// <summary>
    /// Gets a query that lists the tables and views, as rows of schema, name and kind (<c>Table</c> or <c>View</c>).
    /// </summary>
    protected abstract string ListResourcesSql { get; }

    /// <summary>
    /// Gets a query that returns one value describing the server, such as its version.
    /// </summary>
    protected abstract string VersionSql { get; }

    /// <summary>
    /// Returns a query that reads at most a number of rows of a <c>SELECT</c>.
    /// </summary>
    /// <param name="columns">The columns, already quoted and joined.</param>
    /// <param name="fromAndWhere">The <c>FROM</c> and <c>WHERE</c> clauses.</param>
    /// <param name="maxRows">The most rows, or zero for no limit.</param>
    /// <returns>The query.</returns>
    protected virtual string Select(string columns, string fromAndWhere, int maxRows)
        => maxRows > 0
            ? $"SELECT {columns} FROM {fromAndWhere} LIMIT {maxRows}"
            : $"SELECT {columns} FROM {fromAndWhere}";

    /// <summary>
    /// Returns whether a database error may succeed when tried again.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <returns><see langword="true"/> for a transient error.</returns>
    protected virtual bool IsTransient(DbException exception)
        => exception?.IsTransient == true;

    /// <summary>
    /// Returns the data field type of a result column.
    /// </summary>
    /// <param name="column">The column's schema.</param>
    /// <returns>The data field type.</returns>
    protected virtual DataFieldType MapType(DbColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (string.Equals(column.DataTypeName, "date", StringComparison.OrdinalIgnoreCase))
        {
            return DataFieldType.Date;
        }

        var type = Nullable.GetUnderlyingType(column.DataType ?? typeof(string)) ?? column.DataType ?? typeof(string);

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 => DataFieldType.Integer,
            TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => DataFieldType.Decimal,
            TypeCode.Boolean => DataFieldType.Boolean,
            TypeCode.DateTime => DataFieldType.DateTime,
            _ when type == typeof(DateTimeOffset) => DataFieldType.DateTime,
            _ when type == typeof(DateOnly) => DataFieldType.Date,
            _ => DataFieldType.Text,
        };
    }

    /// <summary>
    /// Checks that the connection reaches the database.
    /// </summary>
    /// <param name="context">The connection.</param>
    /// <param name="cancellationToken">The token that cancels the check.</param>
    /// <returns>The result, with the server's version.</returns>
    public async Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await using var connection = await OpenAsync(context, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = VersionSql;
            command.CommandTimeout = 30;

            var version = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            var firstLine = version?.Split('\n', 2)[0].Trim();

            return DataConnectorTestResult.Success($"Connected. {firstLine}");
        }
        catch (DataConnectorException exception)
        {
            return DataConnectorTestResult.Failure(exception.Message);
        }
    }

    /// <summary>
    /// Lists the tables and views of the database.
    /// </summary>
    /// <param name="context">The connection.</param>
    /// <param name="cancellationToken">The token that cancels the listing.</param>
    /// <returns>The tables and views, named <c>schema.name</c>.</returns>
    public async Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resources = new List<DataConnectorResource>();

        await using var connection = await OpenAsync(context, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ListResourcesSql;
        command.CommandTimeout = 60;

        await using var reader = await ExecuteAsync(() => command.ExecuteReaderAsync(cancellationToken));

        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.IsDBNull(0) ? null : reader.GetString(0);
            var name = reader.GetString(1);
            var kind = reader.IsDBNull(2) ? "Table" : reader.GetString(2);
            var qualified = string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

            resources.Add(new DataConnectorResource(qualified, qualified, kind));
        }

        return resources;
    }

    /// <summary>
    /// Returns the fields of a data set from the schema of its result set, without reading rows.
    /// </summary>
    /// <param name="context">The data set.</param>
    /// <param name="cancellationToken">The token that cancels the description.</param>
    /// <returns>The fields, with the primary key columns as identifiers when the database reports them.</returns>
    public async Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.DataSet.GetOrCreate<RelationalDataSetSettings>();

        await using var connection = await OpenAsync(context, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = Math.Clamp(settings.CommandTimeoutSeconds, 5, 3_600);
        command.CommandText = Select("*", $"{GetSource(context, settings)} WHERE 1 = 0", 0);
        BindParameters(command, context.Parameters);

        IReadOnlyList<DbColumn> columns;

        await using (var reader = await ExecuteAsync(() => command.ExecuteReaderAsync(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo, cancellationToken)))
        {
            columns = await reader.GetColumnSchemaAsync(cancellationToken);
        }

        await transaction.RollbackAsync(CancellationToken.None);

        var custom = !string.IsNullOrWhiteSpace(settings.Query);

        return
        [
            .. columns
                .Where(column => !string.IsNullOrEmpty(column.ColumnName) && column.IsHidden != true)
                .Select(column => new DataField(column.ColumnName, column.ColumnName, MapType(column))
                {
                    IsIdentifier = !custom && column.IsKey == true,
                }),
        ];
    }

    /// <summary>
    /// Streams the rows of a data set.
    /// </summary>
    /// <param name="context">The data set, fields, conditions, limit and watermark.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The batches of rows.</returns>
    public async IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.DataSet.GetOrCreate<RelationalDataSetSettings>();
        var batchSize = context.BatchSize > 0 ? context.BatchSize : DataSourceQuery.DefaultBatchSize;

        await using var connection = await OpenAsync(context, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = Math.Clamp(settings.CommandTimeoutSeconds, 5, 3_600);
        command.CommandText = BuildRead(context, settings, command);
        BindParameters(command, context.Parameters);

        await using var reader = await ExecuteAsync(() => command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken));

        var fields = Enumerable.Range(0, reader.FieldCount)
            .Select(index => new DataField(reader.GetName(index), reader.GetName(index), DataFieldType.Text))
            .ToArray();
        var rows = new List<object[]>(batchSize);
        var any = false;

        while (await ReadAsync(reader, cancellationToken))
        {
            var row = new object[fields.Length];

            for (var index = 0; index < fields.Length; index++)
            {
                var value = reader.GetValue(index);
                row[index] = value is DBNull ? null : value;
            }

            rows.Add(row);

            if (rows.Count == batchSize)
            {
                any = true;

                yield return new DataBatch(fields, rows);

                rows = new List<object[]>(batchSize);
            }
        }

        if (rows.Count > 0 || !any)
        {
            yield return new DataBatch(fields, rows);
        }

        await reader.DisposeAsync();
        await transaction.RollbackAsync(CancellationToken.None);
    }

    /// <summary>
    /// Builds the query of a read: the columns wanted, the conditions the database can apply exactly as the consumer
    /// would, the watermark, and the row limit.
    /// </summary>
    internal string BuildRead(DataConnectorReadContext context, RelationalDataSetSettings settings, DbCommand command)
    {
        var columns = context.Fields.Count == 0
            ? "*"
            : string.Join(", ", context.Fields.Select(QuoteIdentifier));
        var where = new List<string>();
        var parameters = new SqlParameters(command);

        foreach (var condition in context.Conditions)
        {
            var sourceType = context.DataSet.Fields?.FirstOrDefault(field => string.Equals(field.SourceName, condition.Field, StringComparison.Ordinal))?.SourceType;

            if (sourceType is { } type && ToSql(condition, type, parameters) is { } clause)
            {
                where.Add(clause);
            }
        }

        if (!string.IsNullOrEmpty(context.WatermarkField) && context.Watermark is not null)
        {
            where.Add($"{QuoteIdentifier(context.WatermarkField)} > {parameters.Bind(context.Watermark)}");
        }

        var from = GetSource(context, settings);

        if (where.Count > 0)
        {
            from += " WHERE " + string.Join(" AND ", where);
        }

        return Select(columns, from, context.MaxRows);
    }

    /// <summary>
    /// Returns the <c>FROM</c> target of a data set: its table or view quoted, or its query as a derived table.
    /// </summary>
    internal string GetSource(DataConnectorReadContext context, RelationalDataSetSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Query))
        {
            return $"({ValidateQuery(settings.Query)}) {QuoteIdentifier(_derivedTableAlias)}";
        }

        var resource = context.DataSet.Resource?.Trim();

        if (string.IsNullOrEmpty(resource))
        {
            throw new DataConnectorException("The data set names no table or view, and has no query.");
        }

        return QuoteName(resource);
    }

    /// <summary>
    /// Quotes a table name of one or two parts, such as <c>dbo.Customers</c>.
    /// </summary>
    internal string QuoteName(string name)
    {
        var parts = name.Split('.', 2, StringSplitOptions.TrimEntries);

        if (parts.Any(string.IsNullOrEmpty))
        {
            throw new DataConnectorException($"'{name}' isn't a table name.");
        }

        return string.Join(".", parts.Select(QuoteIdentifier));
    }

    /// <summary>
    /// Checks that a custom query is a single query expression that can run as a derived table: its parentheses
    /// balance and it has no statement separator, outside of quoted text and comments. A trailing separator is removed.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The query, ready to wrap.</returns>
    /// <exception cref="DataConnectorException">The query could close the derived table or start another statement.</exception>
    public static string ValidateQuery(string query)
    {
        var text = query?.Trim() ?? string.Empty;

        while (text.EndsWith(';'))
        {
            text = text[..^1].TrimEnd();
        }

        if (text.Length == 0)
        {
            throw new DataConnectorException("The query is empty.");
        }

        var depth = 0;
        var code = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            switch (character)
            {
                case '\'':
                case '"':
                case '[':
                case '`':
                    index = SkipQuoted(text, index, character == '[' ? ']' : character);
                    code.Append(' ');
                    continue;

                case '-' when index + 1 < text.Length && text[index + 1] == '-':
                    throw new DataConnectorException("The query can't contain -- comments; use /* */ or remove them.");

                case '/' when index + 1 < text.Length && text[index + 1] == '*':
                    var commentEnd = text.IndexOf("*/", index + 2, StringComparison.Ordinal);

                    if (commentEnd < 0)
                    {
                        throw new DataConnectorException("The query has an unclosed comment.");
                    }

                    index = commentEnd + 1;
                    code.Append(' ');
                    continue;

                case '(':
                    depth++;
                    break;

                case ')':
                    if (--depth < 0)
                    {
                        throw new DataConnectorException("The query closes a parenthesis it didn't open.");
                    }

                    break;

                case ';':
                    throw new DataConnectorException("The query must be a single SELECT, without a second statement.");
            }

            code.Append(character);
        }

        if (depth != 0)
        {
            throw new DataConnectorException("The query's parentheses don't balance.");
        }

        var start = code.ToString().TrimStart();

        if (!start.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || (start.Length > 6 && !char.IsWhiteSpace(start[6]) && start[6] != '*'))
        {
            throw new DataConnectorException("The query must start with SELECT.");
        }

        return text;
    }

    // Returns the position of the quote that closes the quoted text starting at a position; a doubled closing quote
    // is an escaped one.
    private static int SkipQuoted(string text, int start, char close)
    {
        for (var index = start + 1; index < text.Length; index++)
        {
            if (text[index] != close)
            {
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == close)
            {
                index++;

                continue;
            }

            return index;
        }

        throw new DataConnectorException("The query has an unclosed quote.");
    }

    private string ToSql(DataCondition condition, DataFieldType sourceType, SqlParameters parameters)
    {
        var values = (condition.Values ?? []).Where(value => value is not null).ToArray();

        if (values.Length == 0)
        {
            return null;
        }

        var column = QuoteIdentifier(condition.Field);
        var numeric = DataValues.IsNumeric(sourceType) || sourceType == DataFieldType.Boolean;
        var temporal = DataValues.IsTemporal(sourceType);

        // Text isn't compared at the source: a database's collation may not ignore case as the consumer does.
        if (!numeric && !temporal)
        {
            return null;
        }

        return condition.Operator switch
        {
            DataFilterOperator.GreaterThan => $"{column} > {parameters.Bind(values[0])}",
            DataFilterOperator.GreaterThanOrEqual => $"{column} >= {parameters.Bind(values[0])}",
            DataFilterOperator.LessThan => $"{column} < {parameters.Bind(values[0])}",
            DataFilterOperator.LessThanOrEqual => $"{column} <= {parameters.Bind(values[0])}",
            DataFilterOperator.Between when values.Length > 1 => $"{column} BETWEEN {parameters.Bind(values[0])} AND {parameters.Bind(values[1])}",
            DataFilterOperator.Equals when numeric => $"{column} = {parameters.Bind(values[0])}",
            DataFilterOperator.In when numeric && values.Length <= 500 => $"{column} IN ({string.Join(", ", values.Select(parameters.Bind))})",
            _ => null,
        };
    }

    private sealed class SqlParameters
    {
        private readonly DbCommand _command;
        private int _count;

        public SqlParameters(DbCommand command)
        {
            _command = command;
        }

        public string Bind(object value)
        {
            var name = "@p" + _count++;
            AddParameter(_command, name, value);

            return name;
        }
    }

    private static void BindParameters(DbCommand command, IDictionary<string, object> parameters)
    {
        foreach (var (name, value) in parameters ?? new Dictionary<string, object>())
        {
            if (!string.IsNullOrWhiteSpace(name) && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                AddParameter(command, "@" + name, value);
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private async Task<DbConnection> OpenAsync(DataConnectorContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await OpenConnectionAsync(context, cancellationToken);
        }
        catch (DbException exception)
        {
            throw new DataConnectorException($"Couldn't connect to the database: {exception.Message}", exception) { IsTransient = IsTransient(exception) };
        }
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> execute)
    {
        try
        {
            return await execute();
        }
        catch (DbException exception)
        {
            throw new DataConnectorException($"The database refused the query: {exception.Message}", exception) { IsTransient = IsTransient(exception) };
        }
    }

    private async Task<bool> ReadAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            throw new DataConnectorException($"The database failed while reading: {exception.Message}", exception) { IsTransient = IsTransient(exception) };
        }
    }
}
