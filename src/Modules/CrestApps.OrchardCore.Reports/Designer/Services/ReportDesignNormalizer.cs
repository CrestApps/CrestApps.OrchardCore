using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Cleans up designs and views received from the designer before they are validated and saved: missing lists become
/// empty, text is trimmed, widths are clamped, and an exposed filter's operator is made to match its control.
/// </summary>
public static class ReportDesignNormalizer
{
    /// <summary>
    /// The longest title, label, or name accepted.
    /// </summary>
    public const int MaxNameLength = 200;

    /// <summary>
    /// Normalizes a query in place.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The same query, or a new empty one when <paramref name="query"/> is <see langword="null"/>.</returns>
    public static ReportQueryDefinition Normalize(ReportQueryDefinition query)
    {
        query ??= new ReportQueryDefinition();
        query.DataSets = (query.DataSets ?? []).Where(dataSet => dataSet is not null).ToList();
        query.Joins = (query.Joins ?? []).Where(join => join is not null).ToList();
        query.CalculatedFields = (query.CalculatedFields ?? []).Where(field => field is not null).ToList();
        query.Filters = (query.Filters ?? []).Where(filter => filter is not null).ToList();
        query.Columns = (query.Columns ?? []).Where(column => column is not null).ToList();
        query.Sorts = (query.Sorts ?? []).Where(sort => sort is not null).ToList();

        foreach (var dataSet in query.DataSets)
        {
            dataSet.Alias = dataSet.Alias?.Trim();
            dataSet.DisplayName = Truncate(dataSet.DisplayName);
        }

        foreach (var join in query.Joins)
        {
            join.Conditions = (join.Conditions ?? []).Where(condition => condition is not null).ToList();
        }

        query.Joins = MergeJoins(query.Joins);

        foreach (var field in query.CalculatedFields)
        {
            field.Name = field.Name?.Trim();
            field.Label = Truncate(field.Label);
        }

        foreach (var column in query.Columns)
        {
            column.Label = Truncate(column.Label);
            column.Format = Truncate(column.Format);
        }

        foreach (var filter in query.Filters)
        {
            filter.Label = Truncate(filter.Label);
            filter.Values = (filter.Values ?? []).Select(value => value ?? string.Empty).Take(500).ToList();

            if (filter.Exposed)
            {
                filter.Operator = filter.Control switch
                {
                    ReportFilterControl.DateRange or ReportFilterControl.NumberRange => ReportFilterOperator.Between,
                    ReportFilterControl.MultiSelect when filter.Operator != ReportFilterOperator.NotIn => ReportFilterOperator.In,
                    ReportFilterControl.Select when filter.Operator != ReportFilterOperator.NotEquals => ReportFilterOperator.Equals,
                    ReportFilterControl.Boolean => ReportFilterOperator.Equals,
                    _ => filter.Operator,
                };
            }
        }

        return query;
    }

    /// <summary>
    /// Normalizes visuals in place.
    /// </summary>
    /// <param name="visuals">The visuals.</param>
    /// <returns>The cleaned list.</returns>
    public static IList<ReportVisualDefinition> Normalize(IList<ReportVisualDefinition> visuals)
    {
        var list = (visuals ?? []).Where(visual => visual is not null).ToList();

        foreach (var visual in list)
        {
            visual.Title = Truncate(visual.Title);
            visual.Width = Math.Clamp(visual.Width, 1, 12);
            visual.ColumnIds = (visual.ColumnIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToList();
            visual.ValueColumnIds = (visual.ValueColumnIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        return list;
    }

    /// <summary>
    /// Merges the joins that attach the same data set into one, so a design that ended up with two joins for a data set
    /// (the planner needs exactly one) still runs. The first join keeps its type; the matching column pairs of all of
    /// them are kept once each, and pairs with a missing column are dropped when a complete pair exists.
    /// </summary>
    /// <param name="joins">The joins.</param>
    /// <returns>One join per data set, in the order they first appear.</returns>
    public static List<ReportJoinDefinition> MergeJoins(IEnumerable<ReportJoinDefinition> joins)
    {
        var merged = new List<ReportJoinDefinition>();

        foreach (var group in (joins ?? []).Where(join => join is not null).GroupBy(join => join.Alias ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var conditions = group
                .SelectMany(join => join.Conditions ?? [])
                .Where(condition => condition is not null)
                .ToList();
            var complete = conditions
                .Where(condition => !string.IsNullOrEmpty(condition.LeftField) && !string.IsNullOrEmpty(condition.RightField))
                .DistinctBy(condition => (condition.LeftField, condition.RightField))
                .ToList();

            first.Conditions = complete.Count > 0 ? complete : (first.Conditions ?? []).Where(condition => condition is not null).ToList();
            merged.Add(first);
        }

        return merged;
    }

    /// <summary>
    /// Trims text and limits its length.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is empty.</returns>
    public static string Truncate(string value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }
}
