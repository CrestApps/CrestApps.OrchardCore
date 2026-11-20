using System.Globalization;

namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// Tells whether a <see cref="ConditionRule"/> is met. Values compare without regard to case; numbers compare in the
/// invariant culture.
/// </summary>
public static class ConditionEvaluator
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

        if (ConditionRule.IsEmpty(rule))
        {
            return true;
        }

        var judged = 0;
        var met = 0;

        foreach (var condition in rule.Conditions)
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

        // Every condition was left out: the rule can't be judged, so it applies.
        return judged == 0 || (rule.Match == ConditionMatch.All ? met == judged : met > 0);
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
            _ => false,
        };
    }

    private static bool Same(string left, string right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Compare(IReadOnlyList<string> values, string expected, Func<int, bool> test)
    {
        if (!TryNumber(expected, out var target))
        {
            return false;
        }

        return values.Any(item => TryNumber(item, out var number) && test(number.CompareTo(target)));
    }

    private static bool TryNumber(string value, out decimal number)
        => decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out number);
}
