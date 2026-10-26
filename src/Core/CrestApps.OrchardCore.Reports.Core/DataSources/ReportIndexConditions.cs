using System.Linq.Expressions;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Reports.Designer.DataSources;

/// <summary>
/// Applies the conditions of a <see cref="DataSourceQuery"/> that limit a field to a list of values (<c>In</c> and
/// <c>Equals</c>, such as a viewer's channel or status filter) to the index rows a data set reads, so the database
/// returns only the matching records instead of the data set reading every record for the consumer to filter, and a
/// row limit counts only the records that match.
/// </summary>
/// <remarks>
/// The consumer applies every condition again to the rows it reads, comparing text without regard to case, so a
/// condition applied here may keep more rows than it allows, but never fewer: text is compared in upper case, and a
/// condition this can't compare that way, such as one on text that isn't ASCII, is left to the consumer.
/// </remarks>
/// <typeparam name="TIndex">The index the data set reads.</typeparam>
public sealed class ReportIndexConditions<TIndex>
    where TIndex : MapIndex
{
    private readonly IEnumerable<DataCondition> _conditions;
    private readonly List<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>> _filters = [];
    private int _parameterCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportIndexConditions{TIndex}"/> class.
    /// </summary>
    /// <param name="conditions">The conditions of the query.</param>
    public ReportIndexConditions(IEnumerable<DataCondition> conditions)
    {
        _conditions = conditions ?? [];
    }

    /// <summary>
    /// Gets a value indicating whether the conditions allow no value for one of the fields, so no record matches.
    /// </summary>
    public bool MatchesNothing { get; private set; }

    /// <summary>
    /// Limits the index rows to those whose text column holds one of the values the conditions allow for a field,
    /// without regard to case.
    /// </summary>
    /// <param name="field">The name of the field.</param>
    /// <param name="column">The text column of the index that holds the field's values.</param>
    /// <returns>This instance.</returns>
    public ReportIndexConditions<TIndex> Text(string field, string column)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);

        foreach (var keys in GetKeys(field))
        {
            AddText(column, keys);
        }

        return this;
    }

    /// <summary>
    /// Limits the index rows to those whose enumeration column holds one of the values the conditions allow for a
    /// field, by the names of the values without regard to case. The field's values must be the names of the
    /// enumeration's values.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enumeration.</typeparam>
    /// <param name="field">The name of the field.</param>
    /// <param name="column">The column of the index that holds the field's values.</param>
    /// <returns>This instance.</returns>
    public ReportIndexConditions<TIndex> Enum<TEnum>(string field, Expression<Func<TIndex, TEnum>> column)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(column);

        foreach (var keys in GetKeys(field))
        {
            AddEnum(column, keys);
        }

        return this;
    }

    /// <summary>
    /// Applies the conditions to the index rows.
    /// </summary>
    /// <param name="rows">The index rows.</param>
    /// <returns>The index rows, limited by the conditions.</returns>
    public IQueryIndex<TIndex> Apply(IQueryIndex<TIndex> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        foreach (var filter in _filters)
        {
            rows = filter(rows);
        }

        return rows;
    }

    // The values each condition that limits the field to a list of values allows, one list per condition. The lists of
    // several conditions are not intersected here: the consumer compares text without regard to case, and an
    // intersection would not, so each condition is applied on its own and the database keeps the rows all of them allow.
    private IEnumerable<IReadOnlyList<object>> GetKeys(string field)
    {
        foreach (var condition in _conditions)
        {
            if (condition is not null &&
                string.Equals(condition.Field, field, StringComparison.Ordinal) &&
                DataConditions.Keys([condition], field) is { } keys)
            {
                yield return keys;
            }
        }
    }

    private void AddText(string column, IReadOnlyList<object> keys)
    {
        if (keys.Count == 0)
        {
            MatchesNothing = true;

            return;
        }

        var values = new string[keys.Count];

        for (var index = 0; index < keys.Count; index++)
        {
            // Only ASCII text is compared in upper case the same way by every database and by the consumer.
            if (keys[index] is not string text || !text.All(char.IsAscii))
            {
                return;
            }

            values[index] = text.ToUpperInvariant();
        }

        var first = _parameterCount;

        _parameterCount += values.Length;

        var names = values.Select((_, index) => $"{column}_in{first + index}").ToArray();
        var list = string.Join(", ", names.Select(name => "@" + name));

        _filters.Add(rows =>
        {
            for (var index = 0; index < values.Length; index++)
            {
                rows = rows.WithParameter(names[index], values[index]);
            }

            return rows.Where(dialect => $"UPPER({dialect.QuoteForColumnName(column)}) IN ({list})");
        });
    }

    private void AddEnum<TEnum>(Expression<Func<TIndex, TEnum>> column, IReadOnlyList<object> keys)
        where TEnum : struct, Enum
    {
        var values = new HashSet<TEnum>();

        foreach (var key in keys)
        {
            if (key is TEnum value)
            {
                values.Add(value);
            }
            else if (key is string text)
            {
                // A name that isn't one of the enumeration's matches no record.
                if (System.Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
                {
                    values.Add(parsed);
                }
            }
            else
            {
                // A value of another type is compared by the consumer.
                return;
            }
        }

        if (values.Count == 0)
        {
            MatchesNothing = true;

            return;
        }

        Expression body = null;

        foreach (var value in values)
        {
            var equal = Expression.Equal(column.Body, Expression.Constant(value, typeof(TEnum)));

            body = body is null ? equal : Expression.OrElse(body, equal);
        }

        var predicate = Expression.Lambda<Func<TIndex, bool>>(body, column.Parameters);

        _filters.Add(rows => rows.Where(predicate));
    }
}
