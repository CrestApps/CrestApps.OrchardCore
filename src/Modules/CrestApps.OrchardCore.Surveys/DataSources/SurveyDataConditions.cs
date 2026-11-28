using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// Evaluates a condition a consumer passed down to a Surveys data set against one value, so the data set can count
/// an anonymous survey's group sizes on the rows the consumer keeps. It errs on the side of hiding: an operator it
/// doesn't know fails.
/// </summary>
internal static class SurveyDataConditions
{
    /// <summary>
    /// Gets whether a value passes a condition.
    /// </summary>
    /// <param name="value">The value, already of the field's type.</param>
    /// <param name="type">The field's type.</param>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true"/> when it passes.</returns>
    public static bool Matches(object value, DataFieldType type, DataCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var values = (condition.Values ?? []).Select(item => item is null ? null : DataValues.Coerce(item, type)).ToList();
        var first = values.Count > 0 ? values[0] : null;

        return condition.Operator switch
        {
            DataFilterOperator.Equals or DataFilterOperator.In => values.Any(item => item is not null && DataValues.AreEqual(value, item)),
            DataFilterOperator.NotEquals or DataFilterOperator.NotIn => value is not null && !values.Any(item => item is not null && DataValues.AreEqual(value, item)),
            DataFilterOperator.Contains => Text(value, first, (text, part) => text.Contains(part, StringComparison.OrdinalIgnoreCase)),
            DataFilterOperator.NotContains => value is not null && !Text(value, first, (text, part) => text.Contains(part, StringComparison.OrdinalIgnoreCase)),
            DataFilterOperator.StartsWith => Text(value, first, (text, part) => text.StartsWith(part, StringComparison.OrdinalIgnoreCase)),
            DataFilterOperator.EndsWith => Text(value, first, (text, part) => text.EndsWith(part, StringComparison.OrdinalIgnoreCase)),
            DataFilterOperator.GreaterThan => value is not null && first is not null && DataValues.Compare(value, first) > 0,
            DataFilterOperator.GreaterThanOrEqual => value is not null && first is not null && DataValues.Compare(value, first) >= 0,
            DataFilterOperator.LessThan => value is not null && first is not null && DataValues.Compare(value, first) < 0,
            DataFilterOperator.LessThanOrEqual => value is not null && first is not null && DataValues.Compare(value, first) <= 0,
            DataFilterOperator.Between => value is not null
                && (first is null || DataValues.Compare(value, first) >= 0)
                && (values.Count < 2 || values[1] is null || DataValues.Compare(value, values[1]) <= 0),
            DataFilterOperator.IsEmpty => DataValues.IsEmpty(value),
            DataFilterOperator.IsNotEmpty => !DataValues.IsEmpty(value),
            _ => false,
        };
    }

    private static bool Text(object value, object part, Func<string, string, bool> test)
        => value is not null && part is not null && test(DataValues.ToText(value) ?? string.Empty, DataValues.ToText(part) ?? string.Empty);
}
