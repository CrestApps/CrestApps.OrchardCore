using System.Data.Common;
using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Reports.DataSources;
using YesSql;

namespace CrestApps.OrchardCore.Reports.Designer.DataSources;

/// <summary>
/// Answers an <see cref="ReportAggregateQuery"/> with one <c>GROUP BY</c> statement over a YesSql map index table, for
/// data sets whose fields are columns of their index. It declines (returns <see langword="null"/>) whenever it cannot
/// answer exactly the way the report engine would:
/// <list type="bullet">
/// <item><description>a field that is not one of the mapped columns;</description></item>
/// <item><description>a text comparison other than equality, or equality on non-ASCII text (the engine ignores case,
/// and databases only fold the case of ASCII letters alike);</description></item>
/// <item><description>an empty-text test on a text column (the engine treats blank text as empty);</description></item>
/// <item><description>more groups than <see cref="ReportAggregateQuery.MaxGroups"/>.</description></item>
/// </list>
/// Enum columns, which YesSql stores as numbers, are compared and grouped by name. Date buckets are written as a
/// <c>CASE</c> expression over UTC boundaries, which every database understands. The statement runs in the session's
/// own transaction.
/// </summary>
public static class ReportIndexAggregator
{
    /// <summary>
    /// Groups and aggregates the rows of an index table.
    /// </summary>
    /// <typeparam name="TIndex">The map index type.</typeparam>
    /// <param name="session">The YesSql session.</param>
    /// <param name="collection">The collection the index is in, or <see langword="null"/>.</param>
    /// <param name="query">The query.</param>
    /// <param name="columns">The index column of each report field that can be grouped, aggregated, or filtered.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The groups, or <see langword="null"/> when the query cannot be answered exactly.</returns>
    public static async Task<ReportAggregateTable> AggregateAsync<TIndex>(
        ISession session,
        string collection,
        ReportAggregateQuery query,
        IReadOnlyDictionary<string, string> columns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columns);

        var statement = Build<TIndex>(session.Store.Configuration, collection, query, columns);

        if (statement is null)
        {
            return null;
        }

        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await using var command = transaction.Connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = statement.Sql;

        foreach (var (name, value) in statement.Parameters)
        {
            var parameter = command.CreateParameter();

            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        var table = new ReportAggregateTable();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            if (table.Rows.Count >= Math.Max(1, query.MaxGroups))
            {
                return null;
            }

            table.Rows.Add(ReadRow(reader, statement));
        }

        return table;
    }

    /// <summary>
    /// Maps the report fields that have an index column of the same name to that column, for data sets whose index copies
    /// those fields from the record unchanged.
    /// </summary>
    /// <typeparam name="TIndex">The map index type.</typeparam>
    /// <param name="fieldNames">The report field names.</param>
    /// <returns>The index column of each field that has one.</returns>
    public static IReadOnlyDictionary<string, string> MapByName<TIndex>(IEnumerable<string> fieldNames)
    {
        var properties = typeof(TIndex).GetProperties()
            .Where(property => property.Name is not ("Id" or "DocumentId"))
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        return (fieldNames ?? [])
            .Where(properties.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, name => name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Builds the statement, or returns <see langword="null"/> when the query cannot be answered exactly. Public for
    /// tests of the SQL itself.
    /// </summary>
    /// <typeparam name="TIndex">The map index type.</typeparam>
    /// <param name="configuration">The YesSql configuration that names the table and the dialect.</param>
    /// <param name="collection">The collection the index is in, or <see langword="null"/>.</param>
    /// <param name="query">The query.</param>
    /// <param name="columns">The index column of each report field.</param>
    /// <returns>The statement, or <see langword="null"/>.</returns>
    public static ReportIndexStatement Build<TIndex>(
        IConfiguration configuration,
        string collection,
        ReportAggregateQuery query,
        IReadOnlyDictionary<string, string> columns)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columns);

        var dialect = configuration.SqlDialect;
        var table = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(TIndex), collection), configuration.Schema);
        var statement = new ReportIndexStatement();
        var selects = new List<string>();
        var groups = new List<string>();
        var wheres = new List<string>();

        string Parameter(object value)
        {
            var name = "@r" + statement.Parameters.Count.ToString(CultureInfo.InvariantCulture);

            statement.Parameters.Add((name, value));

            return name;
        }

        bool TryColumn(string field, out string quoted, out Type type)
        {
            quoted = null;
            type = null;

            if (field is null || !columns.TryGetValue(field, out var name) || typeof(TIndex).GetProperty(name) is not { } property)
            {
                return false;
            }

            quoted = dialect.QuoteForColumnName(name);
            type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            return true;
        }

        foreach (var group in query.Groups)
        {
            if (!TryColumn(group.Field, out var column, out var type))
            {
                return null;
            }

            string expression;

            if (group.Boundaries is null)
            {
                expression = column;
                statement.Columns.Add(type);
            }
            else
            {
                var builder = new StringBuilder("CASE");
                var names = group.Boundaries.Select(boundary => Parameter(DateTime.SpecifyKind(boundary, DateTimeKind.Utc))).ToArray();

                for (var index = 0; index < names.Length - 1; index++)
                {
                    builder.Append(CultureInfo.InvariantCulture, $" WHEN {column} >= {names[index]} AND {column} < {names[index + 1]} THEN {index}");
                }

                builder.Append(" ELSE NULL END");
                expression = names.Length < 2 ? "NULL" : builder.ToString();
                statement.Columns.Add(typeof(long));
            }

            selects.Add(expression);
            groups.Add(expression);
        }

        foreach (var measure in query.Measures)
        {
            if (measure.Field is null)
            {
                if (measure.Kind != ReportAggregateKind.Count)
                {
                    return null;
                }

                selects.Add("COUNT(*)");
                statement.Columns.Add(typeof(long));

                continue;
            }

            if (!TryColumn(measure.Field, out var column, out var type) ||
                (type.IsEnum && measure.Kind != ReportAggregateKind.Count) ||
                (type == typeof(string) && measure.Kind is ReportAggregateKind.Sum))
            {
                return null;
            }

            selects.Add(measure.Kind switch
            {
                ReportAggregateKind.Count => $"COUNT({column})",
                ReportAggregateKind.Sum => $"SUM({column})",
                ReportAggregateKind.Min => $"MIN({column})",
                _ => $"MAX({column})",
            });
            statement.Columns.Add(measure.Kind == ReportAggregateKind.Count ? typeof(long) : type);
        }

        foreach (var condition in query.Conditions)
        {
            if (!TryColumn(condition.Field, out var column, out var type) ||
                Where(condition, column, type, Parameter) is not { } where)
            {
                return null;
            }

            wheres.Add(where);
        }

        var sql = new StringBuilder("SELECT ")
            .AppendJoin(", ", selects)
            .Append(" FROM ")
            .Append(table);

        if (wheres.Count > 0)
        {
            sql.Append(" WHERE ").AppendJoin(" AND ", wheres);
        }

        if (groups.Count > 0)
        {
            sql.Append(" GROUP BY ").AppendJoin(", ", groups);
        }

        statement.Sql = sql.ToString();

        return statement;
    }

    // One exact condition as SQL, or null when it cannot be written exactly.
    private static string Where(ReportDataCondition condition, string column, Type type, Func<object, string> parameter)
    {
        var isText = type == typeof(string);
        var values = new List<object>();

        foreach (var value in condition.Values ?? [])
        {
            if (!TryConvert(value, type, out var converted))
            {
                // A name that is not a member of the enum matches nothing; any other value that does not fit the column
                // cannot be compared exactly.
                if (type.IsEnum)
                {
                    continue;
                }

                return null;
            }

            values.Add(converted);
        }

        string Compared(object value)
        {
            return isText ? $"UPPER({column}) = {parameter(((string)value).ToUpperInvariant())}" : $"{column} = {parameter(value)}";
        }

        string Listed()
        {
            return isText
                ? $"UPPER({column}) IN ({string.Join(", ", values.Select(value => parameter(((string)value).ToUpperInvariant())))})"
                : $"{column} IN ({string.Join(", ", values.Select(parameter))})";
        }

        switch (condition.Operator)
        {
            case ReportFilterOperator.IsEmpty:
                return isText ? null : $"{column} IS NULL";

            case ReportFilterOperator.IsNotEmpty:
                return isText ? null : $"{column} IS NOT NULL";

            case ReportFilterOperator.Equals:
                return values.Count == 0 ? "1 = 0" : Compared(values[0]);

            case ReportFilterOperator.NotEquals:
                return values.Count == 0 ? "1 = 1" : $"({column} IS NULL OR NOT ({Compared(values[0])}))";

            case ReportFilterOperator.In:
                return values.Count == 0 ? "1 = 0" : Listed();

            case ReportFilterOperator.NotIn:
                return values.Count == 0 ? "1 = 1" : $"({column} IS NULL OR NOT ({Listed()}))";

            case ReportFilterOperator.GreaterThan:
            case ReportFilterOperator.GreaterThanOrEqual:
            case ReportFilterOperator.LessThan:
            case ReportFilterOperator.LessThanOrEqual:
                if (isText || type.IsEnum || type == typeof(bool) || values.Count == 0)
                {
                    return null;
                }

                var comparison = condition.Operator switch
                {
                    ReportFilterOperator.GreaterThan => ">",
                    ReportFilterOperator.GreaterThanOrEqual => ">=",
                    ReportFilterOperator.LessThan => "<",
                    _ => "<=",
                };

                return $"{column} {comparison} {parameter(values[0])}";

            default:
                return null;
        }
    }

    // Converts a condition value to what the column stores: enum names to their numbers, text only when ASCII (see the
    // class remarks), and numbers, booleans, and UTC date-times as they are.
    private static bool TryConvert(object value, Type type, out object converted)
    {
        converted = null;

        if (value is null)
        {
            return false;
        }

        if (type.IsEnum)
        {
            if (Enum.TryParse(type, ReportDataValues.ToText(value), ignoreCase: true, out var member) && Enum.IsDefined(type, member))
            {
                converted = Convert.ToInt32(member, CultureInfo.InvariantCulture);

                return true;
            }

            return false;
        }

        if (type == typeof(string))
        {
            var text = ReportDataValues.ToText(value);

            if (text is null || !Ascii.IsValid(text))
            {
                return false;
            }

            converted = text;

            return true;
        }

        if (type == typeof(DateTime))
        {
            if (ReportDataValues.Coerce(value, ReportDataType.DateTime) is not DateTime date)
            {
                return false;
            }

            converted = date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc);

            return true;
        }

        if (type == typeof(bool))
        {
            converted = ReportDataValues.Coerce(value, ReportDataType.Boolean);

            return converted is not null;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            converted = ReportDataValues.Coerce(value, ReportDataType.Integer);

            return converted is not null;
        }

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            converted = ReportDataValues.Coerce(value, ReportDataType.Decimal);

            return converted is not null;
        }

        return false;
    }

    private static object[] ReadRow(DbDataReader reader, ReportIndexStatement statement)
    {
        var row = new object[reader.FieldCount];

        for (var index = 0; index < row.Length; index++)
        {
            var value = reader.IsDBNull(index) ? null : reader.GetValue(index);
            var type = index < statement.Columns.Count ? statement.Columns[index] : null;

            row[index] = value is not null && type is { IsEnum: true }
                ? Enum.ToObject(type, Convert.ToInt64(value, CultureInfo.InvariantCulture)).ToString()
                : value;
        }

        return row;
    }
}

/// <summary>
/// A grouping statement and its parameters.
/// </summary>
public sealed class ReportIndexStatement
{
    /// <summary>
    /// Gets or sets the SQL.
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// Gets the parameters, by name.
    /// </summary>
    public IList<(string Name, object Value)> Parameters { get; } = [];

    /// <summary>
    /// Gets the CLR type of each selected column, so numbers stored for enums are read back as their names.
    /// </summary>
    public IList<Type> Columns { get; } = [];
}
