using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Reads the values of exposed filters from a report's query string. A filter posts its values as <c>f.{id}</c>, a range
/// as <c>f.{id}.from</c> and <c>f.{id}.to</c>. The form also posts <c>applied=1</c>; without it the report opens with
/// its default filter values.
/// </summary>
public static class ReportFilterValueBinder
{
    /// <summary>
    /// The name of the field that marks a submitted filter form.
    /// </summary>
    public const string AppliedField = "applied";

    /// <summary>
    /// Builds the name of the field that carries a filter value.
    /// </summary>
    /// <param name="filterId">The filter identifier.</param>
    /// <param name="part">The range part (<c>from</c> or <c>to</c>), or <see langword="null"/>.</param>
    /// <returns>The field name.</returns>
    public static string FieldName(string filterId, string part = null)
    {
        return part is null ? $"f.{filterId}" : $"f.{filterId}.{part}";
    }

    /// <summary>
    /// Reads the submitted filter values.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <param name="filters">The report filters.</param>
    /// <returns>The values by filter identifier, or <see langword="null"/> when the filter form was not submitted.</returns>
    public static IDictionary<string, IList<string>> Bind(IQueryCollection query, IEnumerable<ReportFilterDefinition> filters)
    {
        if (query is null || !string.Equals(query[AppliedField], "1", StringComparison.Ordinal))
        {
            return null;
        }

        var values = new Dictionary<string, IList<string>>(StringComparer.Ordinal);

        foreach (var filter in filters ?? [])
        {
            if (filter is null || !filter.Exposed || string.IsNullOrEmpty(filter.Id))
            {
                continue;
            }

            var from = query[FieldName(filter.Id, "from")].ToString();
            var to = query[FieldName(filter.Id, "to")].ToString();

            if (query.ContainsKey(FieldName(filter.Id, "from")) || query.ContainsKey(FieldName(filter.Id, "to")))
            {
                values[filter.Id] = string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)
                    ? []
                    : [from.Trim(), NormalizeUpperBound(to.Trim())];

                continue;
            }

            values[filter.Id] = query[FieldName(filter.Id)]
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Take(500)
                .ToList();
        }

        return values;
    }

    /// <summary>
    /// Turns an upper bound picked as <c>23:59</c> into <c>23:59:59</c> so it keeps the whole last minute.
    /// </summary>
    /// <param name="value">The upper bound.</param>
    /// <returns>The adjusted bound.</returns>
    public static string NormalizeUpperBound(string value)
    {
        if (!string.IsNullOrEmpty(value) && value.EndsWith("T23:59", StringComparison.Ordinal))
        {
            return value + ":59";
        }

        return value;
    }
}
