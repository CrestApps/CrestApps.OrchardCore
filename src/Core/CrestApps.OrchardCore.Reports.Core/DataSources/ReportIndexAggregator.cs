using System.Data.Common;
using System.Globalization;
using System.Text;
using OrchardCore.DataSources;
using YesSql;

namespace CrestApps.OrchardCore.Reports.Designer.DataSources;

/// <summary>
/// Answers an <see cref="DataAggregateQuery"/> with one <c>GROUP BY</c> statement over a YesSql map index table, for
/// data sets whose fields are columns of their index. It declines (returns <see langword="null"/>) whenever it cannot
/// answer exactly the way the report engine would:
/// <list type="bullet">
/// <item><description>a field that is not one of the mapped columns;</description></item>
/// <item><description>a text comparison other than equality, or equality on non-ASCII text (the engine ignores case,
/// and databases only fold the case of ASCII letters alike);</description></item>
/// <item><description>an empty-text test on a text column (the engine treats blank text as empty);</description></item>
/// <item><description>a group by a text column on MySQL, whose default collation also ignores accents and would merge
/// values the engine keeps apart;</description></item>
/// <item><description>the smallest or largest value of a text column (the engine orders text without regard to case,
/// which no database collation matches);</description></item>
/// <item><description>more groups than <see cref="DataAggregateQuery.MaxGroups"/>;</description></item>
/// <item><description>a sum of a <see cref="decimal"/> column on SQLite that could not be made exact (see below).</description></item>
/// </list>
/// Text is grouped by the column itself. The engine groups the rows it gets back again, ignoring case, so a database
/// may keep values that differ only in case apart (PostgreSQL, or non-ASCII letters on SQLite) but must not merge
/// values that differ otherwise. Each text group returns its smallest spelling in ordinal order, which the engine shows.
/// Enum columns, which YesSql stores as numbers, are compared and grouped by name. Date buckets are written as a
/// <c>CASE</c> expression over UTC boundaries, which every database understands. The statement runs in the session's
/// own transaction.
/// <para>
/// SQLite has no exact decimal type: it keeps a <see cref="decimal"/> column as a whole or a floating-point number, so
/// its <c>SUM</c> comes back as a <see cref="double"/> that can differ from the exact sum by a fraction of a cent. On
/// SQLite the statement therefore also reads, per group, the most decimal places a value has, the sum of the absolute
/// values and the count, and the sum is rounded to those places. The rounded sum is the exact decimal sum whenever the
/// worst floating-point error those figures allow is under half of the last place; otherwise the aggregator declines
/// and the engine adds up the exact values of the records. SQL Server, PostgreSQL and MySQL add up their exact decimal
/// columns themselves, so their statements are unchanged.
/// </para>
/// </summary>
public static class ReportIndexAggregator
{
    // The most decimal places a sum is rounded to, the scale a decimal holds.
    private const int MaxDecimalPlaces = 28;

    // The relative error of rounding a number to the nearest double, 2^-53.
    private const double DoubleUnitRoundoff = 1.1102230246251565E-16;

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
    public static async Task<IReadOnlyList<object[]>> AggregateAsync<TIndex>(
        ISession session,
        string collection,
        DataAggregateQuery query,
        IReadOnlyDictionary<string, string> columns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columns);

        var statement = Build<TIndex>(session.Store.Configuration, collection, query, columns);

        if (statement is null)
        {
            ReportTelemetry.RecordIndexAggregate(typeof(TIndex), "unbuildable");

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

        var rows = new List<object[]>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= Math.Max(1, query.MaxGroups))
            {
                ReportTelemetry.RecordIndexAggregate(typeof(TIndex), "too_many_groups");

                return null;
            }

            var row = ReadRow(reader, statement);

            if (row is null)
            {
                ReportTelemetry.RecordIndexAggregate(typeof(TIndex), "inexact_sum");

                return null;
            }

            rows.Add(row);
        }

        ReportTelemetry.RecordIndexAggregate(typeof(TIndex), "built");

        return rows;
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
        DataAggregateQuery query,
        IReadOnlyDictionary<string, string> columns)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columns);

        var dialect = configuration.SqlDialect;
        var table = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(TIndex), collection), configuration.Schema);
        var statement = new ReportIndexStatement();
        var selects = new List<string>();
        var exactSums = new List<(int Column, string Expression)>();
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
            if (!TryColumn(group.Field, out var column, out var type) ||
                (type == typeof(string) && group.Boundaries is null && string.Equals(dialect.Name, "MySql", StringComparison.OrdinalIgnoreCase)))
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

            selects.Add(type == typeof(string) && group.Boundaries is null ? SmallestSpelling(dialect, column) : expression);
            groups.Add(expression);
        }

        foreach (var measure in query.Measures)
        {
            if (measure.Field is null)
            {
                if (measure.Function != DataAggregate.Count)
                {
                    return null;
                }

                selects.Add("COUNT(*)");
                statement.Columns.Add(typeof(long));

                continue;
            }

            if (!TryColumn(measure.Field, out var column, out var type) ||
                (type.IsEnum && measure.Function != DataAggregate.Count) ||
                (type == typeof(string) && measure.Function is DataAggregate.Sum or DataAggregate.Min or DataAggregate.Max))
            {
                return null;
            }

            if (measure.Function == DataAggregate.Sum && type == typeof(decimal) && IsSqlite(dialect))
            {
                exactSums.Add((selects.Count, column));
            }

            selects.Add(measure.Function switch
            {
                DataAggregate.Count => $"COUNT({column})",
                DataAggregate.Sum => $"SUM({Widen(dialect, column, type)})",
                DataAggregate.Min => $"MIN({column})",
                _ => $"MAX({column})",
            });
            statement.Columns.Add(measure.Function == DataAggregate.Count ? typeof(long) : type);
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

        // The figures that make each SQLite decimal sum exact follow the columns of the result.
        foreach (var (position, column) in exactSums)
        {
            statement.DecimalSums.Add(new ReportIndexDecimalSum(position, selects.Count));
            selects.Add(DecimalPlaces(column));
            selects.Add($"SUM(ABS({column}))");
            selects.Add($"COUNT({column})");
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
    private static string Where(DataCondition condition, string column, Type type, Func<object, string> parameter)
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
            case DataFilterOperator.IsEmpty:
                return isText ? null : $"{column} IS NULL";

            case DataFilterOperator.IsNotEmpty:
                return isText ? null : $"{column} IS NOT NULL";

            case DataFilterOperator.Equals:
                return values.Count == 0 ? "1 = 0" : Compared(values[0]);

            case DataFilterOperator.NotEquals:
                return values.Count == 0 ? "1 = 1" : $"({column} IS NULL OR NOT ({Compared(values[0])}))";

            case DataFilterOperator.In:
                return values.Count == 0 ? "1 = 0" : Listed();

            case DataFilterOperator.NotIn:
                return values.Count == 0 ? "1 = 1" : $"({column} IS NULL OR NOT ({Listed()}))";

            case DataFilterOperator.GreaterThan:
            case DataFilterOperator.GreaterThanOrEqual:
            case DataFilterOperator.LessThan:
            case DataFilterOperator.LessThanOrEqual:
                if (isText || type.IsEnum || type == typeof(bool) || values.Count == 0)
                {
                    return null;
                }

                var comparison = condition.Operator switch
                {
                    DataFilterOperator.GreaterThan => ">",
                    DataFilterOperator.GreaterThanOrEqual => ">=",
                    DataFilterOperator.LessThan => "<",
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
            if (Enum.TryParse(type, DataValues.ToText(value), ignoreCase: true, out var member) && Enum.IsDefined(type, member))
            {
                converted = Convert.ToInt32(member, CultureInfo.InvariantCulture);

                return true;
            }

            return false;
        }

        if (type == typeof(string))
        {
            var text = DataValues.ToText(value);

            if (text is null || !Ascii.IsValid(text))
            {
                return false;
            }

            converted = text;

            return true;
        }

        if (type == typeof(DateTime))
        {
            if (DataValues.Coerce(value, DataFieldType.DateTime) is not DateTime date)
            {
                return false;
            }

            converted = date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc);

            return true;
        }

        if (type == typeof(bool))
        {
            converted = DataValues.Coerce(value, DataFieldType.Boolean);

            return converted is not null;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            converted = DataValues.Coerce(value, DataFieldType.Integer);

            return converted is not null;
        }

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            converted = DataValues.Coerce(value, DataFieldType.Decimal);

            return converted is not null;
        }

        return false;
    }

    // The spelling a text group shows: the smallest in ordinal order, as the engine shows it. A database whose collation
    // merges values that differ only in case returns one spelling of each group, so it is asked for the smallest under a
    // binary collation; PostgreSQL keeps the spellings apart and the engine picks among them.
    private static string SmallestSpelling(ISqlDialect dialect, string column)
    {
        return dialect.Name switch
        {
            "Sqlite" => $"MIN({column} COLLATE BINARY)",
            "SqlServer" => $"MIN({column} COLLATE Latin1_General_100_BIN2)",
            _ => column,
        };
    }

    // The most decimal places a value of a column has, as SQLite writes the value as text (at most 15 significant digits,
    // so a value read from a decimal reads as that decimal). A value written with an exponent counts as more places than
    // a decimal holds, so the sum is left to the engine.
    private static string DecimalPlaces(string column)
    {
        var text = $"CAST({column} AS TEXT)";

        return $"MAX(CASE WHEN {text} LIKE '%e%' THEN {MaxDecimalPlaces + 1} WHEN INSTR({text}, '.') > 0 THEN LENGTH({text}) - INSTR({text}, '.') ELSE 0 END)";
    }

    private static bool IsSqlite(ISqlDialect dialect)
    {
        return string.Equals(dialect.Name, "Sqlite", StringComparison.OrdinalIgnoreCase);
    }

    // SQL Server adds up an int or smallint column as an int, which overflows past about two billion, such as the tokens
    // of a busy month; the other databases widen the sum of whole numbers themselves.
    private static string Widen(ISqlDialect dialect, string column, Type type)
    {
        return (type == typeof(int) || type == typeof(short)) && string.Equals(dialect.Name, "SqlServer", StringComparison.OrdinalIgnoreCase)
            ? $"CAST({column} AS BIGINT)"
            : column;
    }

    // A row of the result, or null when a SQLite decimal sum in it cannot be made exact.
    private static object[] ReadRow(DbDataReader reader, ReportIndexStatement statement)
    {
        var width = statement.DecimalSums.Count == 0 ? reader.FieldCount : statement.Columns.Count;
        var row = new object[width];

        for (var index = 0; index < row.Length; index++)
        {
            var value = reader.IsDBNull(index) ? null : reader.GetValue(index);
            var type = index < statement.Columns.Count ? statement.Columns[index] : null;

            row[index] = Normalize(value, type);
        }

        foreach (var sum in statement.DecimalSums)
        {
            var value = reader.IsDBNull(sum.Column) ? null : reader.GetValue(sum.Column);

            if (value is null or long or int)
            {
                // SQLite adds up whole numbers exactly.
                continue;
            }

            if (!TryExactSum(
                Convert.ToDouble(value, CultureInfo.InvariantCulture),
                reader.IsDBNull(sum.DecimalPlacesColumn) ? 0 : Convert.ToInt64(reader.GetValue(sum.DecimalPlacesColumn), CultureInfo.InvariantCulture),
                reader.IsDBNull(sum.MagnitudeColumn) ? 0d : Convert.ToDouble(reader.GetValue(sum.MagnitudeColumn), CultureInfo.InvariantCulture),
                reader.IsDBNull(sum.CountColumn) ? 0 : Convert.ToInt64(reader.GetValue(sum.CountColumn), CultureInfo.InvariantCulture),
                out var exact))
            {
                return null;
            }

            row[sum.Column] = exact;
        }

        return row;
    }

    /// <summary>
    /// Turns the floating-point sum of decimal values, as SQLite returns it, into the exact decimal sum: the values have at
    /// most <paramref name="decimalPlaces"/> places, so their exact sum has no more, and the floating-point sum is rounded
    /// to them. Each value was rounded to the nearest double when stored, and each of the <paramref name="count"/>
    /// additions may round again, so the floating-point sum is within <c>(count + 1)</c> units in the last place of the
    /// double (2<sup>-53</sup>) of <paramref name="magnitude"/>, the sum of the absolute values, of the exact sum. The
    /// rounding gives the exact sum when twice that bound is under half of the last decimal place. Public for tests.
    /// </summary>
    /// <param name="sum">The floating-point sum.</param>
    /// <param name="decimalPlaces">The most decimal places a value has.</param>
    /// <param name="magnitude">The sum of the absolute values.</param>
    /// <param name="count">The number of values added up.</param>
    /// <param name="exact">The exact sum, when it can be told.</param>
    /// <returns><see langword="true"/> when <paramref name="exact"/> is the exact sum.</returns>
    public static bool TryExactSum(double sum, long decimalPlaces, double magnitude, long count, out decimal exact)
    {
        exact = 0m;

        if (!double.IsFinite(sum) || !double.IsFinite(magnitude) || decimalPlaces is < 0 or > MaxDecimalPlaces || count < 0)
        {
            return false;
        }

        var bound = 2d * (count + 1) * Math.Abs(magnitude) * DoubleUnitRoundoff;

        if (bound >= 0.5d * Math.Pow(10, -decimalPlaces))
        {
            return false;
        }

        try
        {
            exact = Math.Round(DecimalConversion.FromDouble(sum), (int)decimalPlaces, MidpointRounding.AwayFromZero);
        }
        catch (OverflowException)
        {
            return false;
        }

        return true;
    }

    // Databases return a column in their own type, such as a boolean or a date as a number or text on SQLite, so each
    // value is given the CLR type its field's type maps to: enums as names, whole numbers as long, other numbers as
    // decimal, booleans as bool, and date-times in UTC.
    private static object Normalize(object value, Type type)
    {
        if (value is null || type is null)
        {
            return value;
        }

        if (type.IsEnum)
        {
            return Enum.ToObject(type, Convert.ToInt64(value, CultureInfo.InvariantCulture)).ToString();
        }

        if (type == typeof(bool))
        {
            return value as bool? ?? Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            return DecimalConversion.FromObject(value);
        }

        if (type == typeof(DateTime))
        {
            var date = value is DateTime dateTime
                ? dateTime
                : DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return value;
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
    /// Gets the CLR type of each column of the result, so numbers stored for enums are read back as their names.
    /// </summary>
    public IList<Type> Columns { get; } = [];

    /// <summary>
    /// Gets the sums of decimal columns that are made exact after they are read (on SQLite), with the columns that
    /// follow those of the result and hold what makes each exact.
    /// </summary>
    public IList<ReportIndexDecimalSum> DecimalSums { get; } = [];
}

/// <summary>
/// A sum of a decimal column that the aggregator makes exact after it is read, and the columns of the statement that
/// hold the figures it needs: the most decimal places a value has, the sum of the absolute values, and the count.
/// </summary>
/// <param name="Column">The column of the result that holds the sum.</param>
/// <param name="DecimalPlacesColumn">The column that holds the most decimal places a value has; the sum of the absolute
/// values and the count follow it.</param>
public sealed record ReportIndexDecimalSum(int Column, int DecimalPlacesColumn)
{
    /// <summary>
    /// Gets the column that holds the sum of the absolute values.
    /// </summary>
    public int MagnitudeColumn => DecimalPlacesColumn + 1;

    /// <summary>
    /// Gets the column that holds the number of values added up.
    /// </summary>
    public int CountColumn => DecimalPlacesColumn + 2;
}
