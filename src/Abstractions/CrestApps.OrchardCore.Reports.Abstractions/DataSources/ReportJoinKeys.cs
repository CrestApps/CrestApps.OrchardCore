namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Reads the join key conditions of a query, for data sources that mark fields
/// <see cref="ReportFieldDescriptor.IsKeyFilterable"/>.
/// </summary>
public static class ReportJoinKeys
{
    /// <summary>
    /// Gets the values a field must have, from the join key conditions on it.
    /// </summary>
    /// <param name="conditions">The conditions of the query.</param>
    /// <param name="field">The field name.</param>
    /// <returns>
    /// The values as text, or <see langword="null"/> when no join key condition applies to the field. An empty list
    /// means no record can match.
    /// </returns>
    public static IReadOnlyList<string> For(IEnumerable<ReportDataCondition> conditions, string field)
    {
        HashSet<string> values = null;

        foreach (var condition in conditions ?? [])
        {
            if (condition is null ||
                !condition.IsJoinKey ||
                condition.Operator != ReportFilterOperator.In ||
                !string.Equals(condition.Field, field, StringComparison.Ordinal))
            {
                continue;
            }

            var keys = (condition.Values ?? [])
                .Select(ReportDataValues.ToText)
                .Where(value => !string.IsNullOrEmpty(value))
                .ToHashSet(StringComparer.Ordinal);

            if (values is null)
            {
                values = keys;
            }
            else
            {
                values.IntersectWith(keys);
            }
        }

        return values?.ToArray();
    }
}
