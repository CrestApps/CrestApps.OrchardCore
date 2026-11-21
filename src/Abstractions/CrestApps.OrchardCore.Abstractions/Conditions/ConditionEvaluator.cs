using System.Globalization;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// Tells whether a <see cref="ConditionRule"/> is met. Values compare without regard to case; numbers and ISO 8601 dates
/// compare in the invariant culture. A browser mirror lives in the Resources module (conditions.js, resource
/// <c>crestapps-conditions</c>); both run the shared vectors in <c>tests/fixtures/conditions</c>.
/// </summary>
public static partial class ConditionEvaluator
{
    /// <summary>
    /// Tells whether a rule is met.
    /// </summary>
    /// <param name="rule">The rule, or <see langword="null"/> for one that always applies.</param>
    /// <param name="lookup">
    /// Reads a source. It returns <see langword="null"/> for a source it doesn't know (a removed field, say): such a
    /// condition is left out, so a stale rule never hides what it can no longer judge. A known source without a value
    /// returns <see cref="ConditionValue.Empty"/>.
    /// </param>
    /// <returns><see langword="true"/> when the rule is met.</returns>
    public static bool IsMet(ConditionRule rule, Func<string, ConditionValue> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        // A rule that can't be judged (every condition left out) applies.
        return Judge(rule, lookup) ?? true;
    }

    /// <summary>
    /// Tells whether one condition is met by a source's value.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="value">What the source reads as.</param>
    /// <returns><see langword="true"/> when it is met.</returns>
    public static bool IsMet(Condition condition, ConditionValue value)
    {
        ArgumentNullException.ThrowIfNull(condition);

        value ??= ConditionValue.Empty;

        if (value.IsWithheld)
        {
            return false;
        }

        var values = value.Values;
        var expected = condition.Values?.FirstOrDefault();
        var all = condition.Values ?? [];

        return condition.Operator switch
        {
            ConditionOperator.Is => expected is not null && values.Any(item => Same(item, expected)),
            ConditionOperator.IsNot => expected is null || !values.Any(item => Same(item, expected)),
            ConditionOperator.IsOneOf => values.Any(item => all.Any(target => Same(item, target))),
            ConditionOperator.IsNoneOf => !values.Any(item => all.Any(target => Same(item, target))),
            ConditionOperator.IsEmpty => !values.Any(item => !string.IsNullOrWhiteSpace(item)),
            ConditionOperator.IsNotEmpty => values.Any(item => !string.IsNullOrWhiteSpace(item)),
            ConditionOperator.Contains => !string.IsNullOrEmpty(expected) && values.Any(item => item.Contains(expected, StringComparison.OrdinalIgnoreCase)),
            ConditionOperator.GreaterThan => Compare(values, expected, comparison => comparison > 0),
            ConditionOperator.LessThan => Compare(values, expected, comparison => comparison < 0),
            ConditionOperator.IsOrUnder => expected is not null
                && (values.Any(item => Same(item, expected)) || value.Ancestors.Any(item => Same(item, expected))),
            ConditionOperator.NotContains => string.IsNullOrEmpty(expected) || !values.Any(item => item.Contains(expected, StringComparison.OrdinalIgnoreCase)),
            ConditionOperator.StartsWith => !string.IsNullOrWhiteSpace(expected)
                && values.Any(item => item.Trim().StartsWith(expected.Trim(), StringComparison.OrdinalIgnoreCase)),
            ConditionOperator.EndsWith => !string.IsNullOrWhiteSpace(expected)
                && values.Any(item => item.Trim().EndsWith(expected.Trim(), StringComparison.OrdinalIgnoreCase)),
            ConditionOperator.GreaterThanOrEqual => Compare(values, expected, comparison => comparison >= 0),
            ConditionOperator.LessThanOrEqual => Compare(values, expected, comparison => comparison <= 0),
            ConditionOperator.Between => all.Count >= 2 && IsBetween(values, all[0], all[1]),
            ConditionOperator.CountAtLeast => TryCount(expected, out var least) && Count(values) >= least,
            ConditionOperator.CountAtMost => TryCount(expected, out var most) && Count(values) <= most,
            _ => false,
        };
    }

    // Judges a rule: null when nothing in it can be judged, so a parent rule leaves it out as it would a condition on
    // an unknown source.
    private static bool? Judge(ConditionRule rule, Func<string, ConditionValue> lookup)
    {
        if (ConditionRule.IsEmpty(rule))
        {
            return null;
        }

        var judged = 0;
        var met = 0;

        foreach (var condition in rule.Conditions ?? [])
        {
            if (string.IsNullOrWhiteSpace(condition?.Source) || lookup(condition.Source) is not { } value)
            {
                continue;
            }

            judged++;

            if (IsMet(condition, value))
            {
                met++;

                if (rule.Match == ConditionMatch.Any)
                {
                    return true;
                }
            }
            else if (rule.Match == ConditionMatch.All)
            {
                return false;
            }
        }

        foreach (var group in rule.Groups ?? [])
        {
            if (Judge(group, lookup) is not { } groupMet)
            {
                continue;
            }

            judged++;

            if (groupMet)
            {
                met++;

                if (rule.Match == ConditionMatch.Any)
                {
                    return true;
                }
            }
            else if (rule.Match == ConditionMatch.All)
            {
                return false;
            }
        }

        if (judged == 0)
        {
            return null;
        }

        return rule.Match == ConditionMatch.All ? met == judged : met > 0;
    }

    private static bool Same(string left, string right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Compare(IReadOnlyList<string> values, string expected, Func<int, bool> test)
    {
        if (TryNumber(expected, out var target))
        {
            return values.Any(item => TryNumber(item, out var number) && test(number.CompareTo(target)));
        }

        if (TryDate(expected, out var date))
        {
            return values.Any(item => TryDate(item, out var itemDate) && test(itemDate.CompareTo(date)));
        }

        return false;
    }

    // The bounds may come in either order; both are included.
    private static bool IsBetween(IReadOnlyList<string> values, string first, string second)
    {
        if (TryNumber(first, out var low) && TryNumber(second, out var high))
        {
            if (low > high)
            {
                (low, high) = (high, low);
            }

            return values.Any(item => TryNumber(item, out var number) && number >= low && number <= high);
        }

        if (TryDate(first, out var from) && TryDate(second, out var to))
        {
            if (from > to)
            {
                (from, to) = (to, from);
            }

            return values.Any(item => TryDate(item, out var date) && date >= from && date <= to);
        }

        return false;
    }

    private static int Count(IReadOnlyList<string> values)
        => values.Count(item => !string.IsNullOrWhiteSpace(item));

    private static bool TryCount(string value, out int count)
        => int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count);

    private static bool TryNumber(string value, out decimal number)
        => decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out number);

    // An ISO 8601 date (2026-01-31) or date and time (2026-01-31T09:30, with seconds, a fraction and an offset or Z
    // when given). A value without an offset is read as UTC, as the browser evaluator reads it.
    private static bool TryDate(string value, out DateTimeOffset date)
    {
        date = default;

        var text = value?.Trim();

        if (string.IsNullOrEmpty(text) || !IsoDate().IsMatch(text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();
}
