using System.Globalization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Turns the values people pick by clicking a visual into keys the report page sends back, and those keys into the
/// filter that narrows other visuals to them.
/// </summary>
public static class ReportSelectionKeys
{
    /// <summary>
    /// The most values a visual's selection keeps.
    /// </summary>
    public const int MaxKeys = 100;

    /// <summary>
    /// The longest key kept.
    /// </summary>
    public const int MaxKeyLength = 500;

    /// <summary>
    /// The prefix of the query string fields that hold a visual's picked values, followed by the visual identifier.
    /// </summary>
    public const string FieldPrefix = "sel.";

    /// <summary>
    /// Tells whether a column can be clicked to filter other visuals: its values must map back to the values of its
    /// field. Text changed in case still matches, since text comparisons ignore case; dates kept to their day, week,
    /// month, year or hour cover that period.
    /// </summary>
    /// <param name="transform">The column's transform.</param>
    /// <returns><see langword="true"/> when the column's values can filter its field.</returns>
    public static bool Supports(DataFieldTransform transform)
    {
        return transform is DataFieldTransform.None
            or DataFieldTransform.Upper
            or DataFieldTransform.Lower
            or DataFieldTransform.Day
            or DataFieldTransform.Week
            or DataFieldTransform.Month
            or DataFieldTransform.Year
            or DataFieldTransform.Hour;
    }

    /// <summary>
    /// Writes a value as a key: text as it is, numbers and dates in the invariant culture, and an empty string for a
    /// missing value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The key.</returns>
    public static string ToKey(object value)
    {
        var key = value switch
        {
            null => string.Empty,
            string text => text,
            bool flag => flag ? "true" : "false",
            DateTime date => date.TimeOfDay == TimeSpan.Zero
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

        return key.Length > MaxKeyLength ? key[..MaxKeyLength] : key;
    }

    /// <summary>
    /// Builds the filter that keeps the rows whose field has one of the picked values. A missing value keeps the rows
    /// without one when it is the only value picked. A date column kept to a period keeps the rows from the start of the
    /// first period picked to the end of the last.
    /// </summary>
    /// <param name="id">The identifier of the filter.</param>
    /// <param name="field">The key of the field filtered.</param>
    /// <param name="transform">The transform of the column the values were picked from.</param>
    /// <param name="keys">The picked values, as keys.</param>
    /// <returns>The filter, or <see langword="null"/> when the values cannot filter the field.</returns>
    public static ReportFilterDefinition ToFilter(string id, string field, DataFieldTransform transform, IEnumerable<string> keys)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var values = (keys ?? []).Where(key => key is not null).Distinct(StringComparer.Ordinal).Take(MaxKeys).ToList();

        if (string.IsNullOrEmpty(field) || values.Count == 0 || !Supports(transform))
        {
            return null;
        }

        var filter = new ReportFilterDefinition
        {
            Id = id,
            Field = field,
            Stage = ReportFilterStage.Rows,
        };

        var present = values.Where(value => value.Length > 0).ToList();

        if (present.Count == 0)
        {
            filter.Operator = DataFilterOperator.IsEmpty;

            return filter;
        }

        if (transform is DataFieldTransform.None or DataFieldTransform.Upper or DataFieldTransform.Lower)
        {
            filter.Operator = present.Count == 1 ? DataFilterOperator.Equals : DataFilterOperator.In;
            filter.Values = present;

            return filter;
        }

        var ranges = present.Select(key => Period(transform, key)).Where(range => range is not null).ToList();

        if (ranges.Count == 0)
        {
            return null;
        }

        filter.Operator = DataFilterOperator.Between;
        filter.Values = [ranges.MinBy(range => range.Value.From).Value.FromText, ranges.MaxBy(range => range.Value.To).Value.ToText];

        return filter;
    }

    // The period a date key kept to a day, week, month, year or hour covers, as the inclusive bounds a filter compares.
    // A date written without a time covers its whole day.
    private static (DateTime From, DateTime To, string FromText, string ToText)? Period(DataFieldTransform transform, string key)
    {
        if (transform == DataFieldTransform.Year)
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) || year < 1 || year > 9999)
            {
                return null;
            }

            var first = new DateTime(year, 1, 1);
            var last = new DateTime(year, 12, 31);

            return (first, last, Day(first), Day(last));
        }

        if (!DateTime.TryParse(key, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        switch (transform)
        {
            case DataFieldTransform.Day:
                return (date.Date, date.Date, Day(date), Day(date));

            case DataFieldTransform.Week:
                return (date.Date, date.Date.AddDays(6), Day(date), Day(date.AddDays(6)));

            case DataFieldTransform.Month:
                var start = new DateTime(date.Year, date.Month, 1);
                var end = start.AddMonths(1).AddDays(-1);

                return (start, end, Day(start), Day(end));

            case DataFieldTransform.Hour:
                var hour = date.Date.AddHours(date.Hour);
                var toText = hour.ToString("yyyy-MM-dd'T'HH", CultureInfo.InvariantCulture) + ":59:59";

                return (hour, hour.AddHours(1).AddTicks(-1), hour.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture), toText);

            default:
                return null;
        }
    }

    private static string Day(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
