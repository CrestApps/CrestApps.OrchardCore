using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Builds the predicates that apply report filters. Text comparisons ignore case. A date value written without a time
/// (<c>2026-01-31</c>) covers that whole day, so "on or before 2026-01-31" keeps the evening of the 31st.
/// </summary>
public static class ReportFilterPredicates
{
    /// <summary>
    /// Builds the predicate of a filter.
    /// </summary>
    /// <param name="filterOperator">The comparison operator.</param>
    /// <param name="dataType">The type of the values compared.</param>
    /// <param name="rawValues">The filter values as invariant text.</param>
    /// <param name="today">The current date in the tenant time zone.</param>
    /// <returns>
    /// The predicate, or <see langword="null"/> when the filter is off because the operator needs values and none were
    /// given.
    /// </returns>
    public static Func<object, bool> Build(
        ReportFilterOperator filterOperator,
        ReportDataType dataType,
        IList<string> rawValues,
        DateTime today)
    {
        var valueType = filterOperator is ReportFilterOperator.InLastDays or ReportFilterOperator.InNextDays
            ? ReportDataType.Integer
            : dataType;
        var values = (rawValues ?? [])
            .Select(raw => Parse(raw, valueType))
            .ToList();
        var present = values.Where(value => value.Value is not null).ToList();
        var first = present.FirstOrDefault();

        switch (filterOperator)
        {
            case ReportFilterOperator.IsEmpty:
                return ReportDataValues.IsEmpty;

            case ReportFilterOperator.IsNotEmpty:
                return value => !ReportDataValues.IsEmpty(value);

            case ReportFilterOperator.Between:
                var lower = values.Count > 0 ? values[0] : default;
                var upper = values.Count > 1 ? values[1] : default;

                if (lower.Value is null && upper.Value is null)
                {
                    return null;
                }

                var lowerBound = LowerBound(lower);
                var upperBound = UpperBound(upper);

                return value => value is not null && lowerBound(value) && upperBound(value);
        }

        if (present.Count == 0)
        {
            return null;
        }

        switch (filterOperator)
        {
            case ReportFilterOperator.Equals:
                return value => Matches(value, first);

            case ReportFilterOperator.NotEquals:
                return value => !Matches(value, first);

            case ReportFilterOperator.In:
                return value => present.Any(candidate => Matches(value, candidate));

            case ReportFilterOperator.NotIn:
                return value => !present.Any(candidate => Matches(value, candidate));

            case ReportFilterOperator.Contains:
                return value => Text(value)?.Contains(first.Text, StringComparison.OrdinalIgnoreCase) == true;

            case ReportFilterOperator.NotContains:
                return value => Text(value)?.Contains(first.Text, StringComparison.OrdinalIgnoreCase) != true;

            case ReportFilterOperator.StartsWith:
                return value => Text(value)?.StartsWith(first.Text, StringComparison.OrdinalIgnoreCase) == true;

            case ReportFilterOperator.EndsWith:
                return value => Text(value)?.EndsWith(first.Text, StringComparison.OrdinalIgnoreCase) == true;

            case ReportFilterOperator.GreaterThan:
                return first.IsDateOnly
                    ? value => value is not null && ReportDataValues.Compare(value, NextDay(first.Value)) >= 0
                    : value => value is not null && ReportDataValues.Compare(value, first.Value) > 0;

            case ReportFilterOperator.GreaterThanOrEqual:
                return LowerBound(first);

            case ReportFilterOperator.LessThan:
                return value => value is not null && ReportDataValues.Compare(value, first.Value) < 0;

            case ReportFilterOperator.LessThanOrEqual:
                return UpperBound(first);

            case ReportFilterOperator.InLastDays:
            case ReportFilterOperator.InNextDays:
                if (ReportDataValues.Coerce(first.Text, ReportDataType.Integer) is not long days || days < 1 || days > 36_500)
                {
                    return null;
                }

                var start = filterOperator == ReportFilterOperator.InLastDays ? today.Date.AddDays(1 - days) : today.Date;
                var end = filterOperator == ReportFilterOperator.InLastDays ? today.Date.AddDays(1) : today.Date.AddDays(days);

                return value => value is DateTime date && date >= start && date < end;

            default:
                return null;
        }
    }

    /// <summary>
    /// Builds a condition a data source may apply while reading, or <see langword="null"/> when the filter cannot be
    /// passed down safely. A passed-down condition never removes a row the full filter would keep; the engine applies
    /// the full filter again afterwards.
    /// </summary>
    /// <param name="fieldName">The data set field name.</param>
    /// <param name="filterOperator">The comparison operator.</param>
    /// <param name="dataType">The type of the field.</param>
    /// <param name="rawValues">The filter values as invariant text.</param>
    /// <param name="toUtc">Converts a tenant-local date-time to UTC.</param>
    /// <param name="today">The tenant-local current date-time, which relative date filters count from.</param>
    /// <returns>The condition, or <see langword="null"/>.</returns>
    public static ReportDataCondition BuildCondition(
        string fieldName,
        ReportFilterOperator filterOperator,
        ReportDataType dataType,
        IList<string> rawValues,
        Func<DateTime, DateTime> toUtc,
        DateTime? today = null)
    {
        ArgumentNullException.ThrowIfNull(toUtc);

        if (filterOperator is ReportFilterOperator.InLastDays or ReportFilterOperator.InNextDays)
        {
            return RelativeCondition(fieldName, filterOperator, dataType, rawValues, toUtc, today);
        }

        var values = (rawValues ?? []).Select(raw => Parse(raw, dataType)).ToList();

        if (ReportDataValues.IsTemporal(dataType))
        {
            object Convert(ParsedValue parsed, bool upper)
            {
                if (parsed.Value is not DateTime date)
                {
                    return null;
                }

                if (upper && parsed.IsDateOnly)
                {
                    date = date.AddDays(1);
                }

                return dataType == ReportDataType.DateTime ? toUtc(date) : date;
            }

            return filterOperator switch
            {
                ReportFilterOperator.GreaterThan or ReportFilterOperator.GreaterThanOrEqual when values.Count > 0 && values[0].Value is not null
                    => Condition(fieldName, ReportFilterOperator.GreaterThanOrEqual, Convert(values[0], upper: false)),
                ReportFilterOperator.LessThan or ReportFilterOperator.LessThanOrEqual when values.Count > 0 && values[0].Value is not null
                    => Condition(fieldName, ReportFilterOperator.LessThanOrEqual, Convert(values[0], upper: true)),
                ReportFilterOperator.Between when values.Any(value => value.Value is not null)
                    => Condition(
                        fieldName,
                        ReportFilterOperator.Between,
                        values.Count > 0 ? Convert(values[0], upper: false) : null,
                        values.Count > 1 ? Convert(values[1], upper: true) : null),
                _ => null,
            };
        }

        var present = values.Where(value => value.Value is not null).Select(value => value.Value).ToArray();

        return filterOperator switch
        {
            ReportFilterOperator.Equals or ReportFilterOperator.In when present.Length > 0
                => Condition(fieldName, ReportFilterOperator.In, present),
            ReportFilterOperator.Contains or ReportFilterOperator.StartsWith or ReportFilterOperator.EndsWith when dataType == ReportDataType.Text && present.Length > 0
                => Condition(fieldName, filterOperator, present[0]),
            ReportFilterOperator.GreaterThan or
            ReportFilterOperator.GreaterThanOrEqual or
            ReportFilterOperator.LessThan or
            ReportFilterOperator.LessThanOrEqual when ReportDataValues.IsNumeric(dataType) && present.Length > 0
                => Condition(fieldName, filterOperator, present[0]),
            ReportFilterOperator.Between when ReportDataValues.IsNumeric(dataType) && present.Length > 0
                => Condition(fieldName, filterOperator, values.Count > 0 ? values[0].Value : null, values.Count > 1 ? values[1].Value : null),
            ReportFilterOperator.IsNotEmpty => Condition(fieldName, filterOperator),
            _ => null,
        };
    }

    /// <summary>
    /// Builds the conditions that keep exactly the rows the filter keeps, for a data source that groups and aggregates
    /// itself (see <see cref="IReportAggregateDataSource"/>), where a loose condition would change the totals.
    /// </summary>
    /// <param name="fieldName">The data set field name.</param>
    /// <param name="filterOperator">The comparison operator.</param>
    /// <param name="dataType">The type of the field.</param>
    /// <param name="rawValues">The filter values as invariant text.</param>
    /// <param name="toUtc">Converts a tenant-local date-time to UTC.</param>
    /// <param name="today">The tenant-local current date-time.</param>
    /// <returns>
    /// The conditions, an empty list when the filter is off, or <see langword="null"/> when the filter cannot be
    /// expressed exactly (text matching such as Contains, or a date-only equality inside a list).
    /// </returns>
    public static IReadOnlyList<ReportDataCondition> BuildExactConditions(
        string fieldName,
        ReportFilterOperator filterOperator,
        ReportDataType dataType,
        IList<string> rawValues,
        Func<DateTime, DateTime> toUtc,
        DateTime today)
    {
        ArgumentNullException.ThrowIfNull(toUtc);

        if (Build(filterOperator, dataType, rawValues, today) is null)
        {
            return [];
        }

        var temporal = ReportDataValues.IsTemporal(dataType);
        var values = (rawValues ?? []).Select(raw => Parse(raw, dataType)).ToList();
        var present = values.Where(value => value.Value is not null).ToList();
        var first = present.FirstOrDefault();

        object Bound(object value)
        {
            return dataType == ReportDataType.DateTime && value is DateTime date ? toUtc(date) : value;
        }

        ReportDataCondition On(ReportFilterOperator op, params object[] conditionValues)
        {
            return Condition(fieldName, op, conditionValues.Select(Bound).ToArray());
        }

        switch (filterOperator)
        {
            case ReportFilterOperator.IsEmpty:
            case ReportFilterOperator.IsNotEmpty:
                return [Condition(fieldName, filterOperator)];

            case ReportFilterOperator.Between:
                {
                    var lower = values.Count > 0 ? values[0] : default;
                    var upper = values.Count > 1 ? values[1] : default;
                    var conditions = new List<ReportDataCondition> { Condition(fieldName, ReportFilterOperator.IsNotEmpty) };

                    if (lower.Value is not null)
                    {
                        conditions.Add(On(ReportFilterOperator.GreaterThanOrEqual, lower.Value));
                    }

                    if (upper.Value is not null)
                    {
                        conditions.Add(upper.IsDateOnly
                            ? On(ReportFilterOperator.LessThan, NextDay(upper.Value))
                            : On(ReportFilterOperator.LessThanOrEqual, upper.Value));
                    }

                    return conditions;
                }

            case ReportFilterOperator.Equals:
                return temporal && first.IsDateOnly
                    ? [On(ReportFilterOperator.GreaterThanOrEqual, first.Value), On(ReportFilterOperator.LessThan, NextDay(first.Value))]
                    : [On(ReportFilterOperator.Equals, first.Value)];

            case ReportFilterOperator.NotEquals:
            case ReportFilterOperator.In:
            case ReportFilterOperator.NotIn:
                if (present.Any(value => value.IsDateOnly))
                {
                    return null;
                }

                return filterOperator == ReportFilterOperator.NotEquals
                    ? [On(ReportFilterOperator.NotEquals, first.Value)]
                    : [On(filterOperator, present.Select(value => value.Value).ToArray())];

            case ReportFilterOperator.GreaterThan:
                return first.IsDateOnly
                    ? [On(ReportFilterOperator.GreaterThanOrEqual, NextDay(first.Value))]
                    : [On(ReportFilterOperator.GreaterThan, first.Value)];

            case ReportFilterOperator.GreaterThanOrEqual:
                return [On(ReportFilterOperator.GreaterThanOrEqual, first.Value)];

            case ReportFilterOperator.LessThan:
                return [On(ReportFilterOperator.LessThan, first.Value)];

            case ReportFilterOperator.LessThanOrEqual:
                return first.IsDateOnly
                    ? [On(ReportFilterOperator.LessThan, NextDay(first.Value))]
                    : [On(ReportFilterOperator.LessThanOrEqual, first.Value)];

            case ReportFilterOperator.InLastDays:
            case ReportFilterOperator.InNextDays:
                {
                    var days = (long)ReportDataValues.Coerce(rawValues.First(value => !string.IsNullOrWhiteSpace(value)).Trim(), ReportDataType.Integer);
                    var start = filterOperator == ReportFilterOperator.InLastDays ? today.Date.AddDays(1 - days) : today.Date;
                    var end = filterOperator == ReportFilterOperator.InLastDays ? today.Date.AddDays(1) : today.Date.AddDays(days);

                    return [On(ReportFilterOperator.GreaterThanOrEqual, start), On(ReportFilterOperator.LessThan, end)];
                }

            default:
                return null;
        }
    }

    // The range of a relative date filter, as the same days the full filter keeps: from the start of the first day to the
    // start of the day after the last one, as an inclusive range, which keeps every row the filter keeps.
    private static ReportDataCondition RelativeCondition(
        string fieldName,
        ReportFilterOperator filterOperator,
        ReportDataType dataType,
        IList<string> rawValues,
        Func<DateTime, DateTime> toUtc,
        DateTime? today)
    {
        if (today is null ||
            !ReportDataValues.IsTemporal(dataType) ||
            ReportDataValues.Coerce(rawValues?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim(), ReportDataType.Integer) is not long days ||
            days < 1 ||
            days > 36_500)
        {
            return null;
        }

        var start = filterOperator == ReportFilterOperator.InLastDays ? today.Value.Date.AddDays(1 - days) : today.Value.Date;
        var end = filterOperator == ReportFilterOperator.InLastDays ? today.Value.Date.AddDays(1) : today.Value.Date.AddDays(days);

        return dataType == ReportDataType.DateTime
            ? Condition(fieldName, ReportFilterOperator.Between, toUtc(start), toUtc(end))
            : Condition(fieldName, ReportFilterOperator.Between, start, end);
    }

    private static ReportDataCondition Condition(string fieldName, ReportFilterOperator filterOperator, params object[] values)
    {
        return new ReportDataCondition
        {
            Field = fieldName,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static Func<object, bool> LowerBound(ParsedValue bound)
    {
        if (bound.Value is null)
        {
            return _ => true;
        }

        return value => value is not null && ReportDataValues.Compare(value, bound.Value) >= 0;
    }

    private static Func<object, bool> UpperBound(ParsedValue bound)
    {
        if (bound.Value is null)
        {
            return _ => true;
        }

        if (bound.IsDateOnly)
        {
            var next = NextDay(bound.Value);

            return value => value is not null && ReportDataValues.Compare(value, next) < 0;
        }

        return value => value is not null && ReportDataValues.Compare(value, bound.Value) <= 0;
    }

    private static bool Matches(object value, ParsedValue candidate)
    {
        if (candidate.IsDateOnly && value is DateTime date && candidate.Value is DateTime day)
        {
            return date.Date == day.Date;
        }

        if (value is null)
        {
            return false;
        }

        return ReportDataValues.AreEqual(value, candidate.Value);
    }

    private static object NextDay(object value)
    {
        return value is DateTime date ? date.Date.AddDays(1) : value;
    }

    private static string Text(object value)
    {
        return ReportDataValues.ToText(value);
    }

    private static ParsedValue Parse(string raw, ReportDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return default;
        }

        var trimmed = raw.Trim();
        var value = ReportDataValues.Coerce(trimmed, dataType);
        var isDateOnly = ReportDataValues.IsTemporal(dataType) &&
            value is DateTime &&
            !trimmed.Contains(':', StringComparison.Ordinal);

        return new ParsedValue(value, trimmed, isDateOnly);
    }

    private readonly record struct ParsedValue(object Value, string Text, bool IsDateOnly);
}
