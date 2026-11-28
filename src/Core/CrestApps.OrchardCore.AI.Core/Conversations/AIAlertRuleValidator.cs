using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The rules an alert rule must meet before it is saved, shared by the alert rule editor and the AI tools.
/// </summary>
public static class AIAlertRuleValidator
{
    /// <summary>
    /// The longest window a rule can watch, in minutes (30 days).
    /// </summary>
    public const int MaxWindowMinutes = 43200;

    /// <summary>
    /// The longest time between two checks, in minutes (a day).
    /// </summary>
    public const int MaxCheckEveryMinutes = 1440;

    /// <summary>
    /// Checks a rule with the values it is about to be saved with.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="S">The localizer of the caller, which words the errors.</param>
    /// <returns>An error per broken rule, named after the property it is about; empty when the rule may be saved.</returns>
    public static IReadOnlyList<ValidationResult> Validate(AIAlertRule rule, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<ValidationResult>();

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            errors.Add(new ValidationResult(S["The name is required."], [nameof(AIAlertRule.Name)]));
        }

        if (rule.WindowMinutes is < 1 or > MaxWindowMinutes)
        {
            errors.Add(new ValidationResult(S["The window must be between 1 minute and 30 days."], [nameof(AIAlertRule.WindowMinutes)]));
        }

        if (rule.CheckEveryMinutes is < 1 or > MaxCheckEveryMinutes)
        {
            errors.Add(new ValidationResult(S["Check between every minute and once a day."], [nameof(AIAlertRule.CheckEveryMinutes)]));
        }

        if (rule.MinimumSampleSize < 0)
        {
            errors.Add(new ValidationResult(S["The minimum sample cannot be negative."], [nameof(AIAlertRule.MinimumSampleSize)]));
        }

        return errors;
    }

    /// <summary>
    /// Gets what a rule watches. When it changes, the rule is checked again at once.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The metric, condition, threshold and profile the rule watches.</returns>
    public static (AIConversationMetric Metric, AIConversationAlertCondition Condition, double Threshold, string ProfileId) GetWatched(AIAlertRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return (rule.Metric, rule.Condition, rule.Threshold, string.IsNullOrWhiteSpace(rule.ProfileId) ? null : rule.ProfileId);
    }

    /// <summary>
    /// Makes a rule whose watched values changed due for a check at once.
    /// </summary>
    /// <param name="rule">The rule with its new values.</param>
    /// <param name="watchedBefore">What it watched before, from <see cref="GetWatched"/>.</param>
    public static void ResetCheckWhenWatchChanged(AIAlertRule rule, (AIConversationMetric Metric, AIConversationAlertCondition Condition, double Threshold, string ProfileId) watchedBefore)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (GetWatched(rule) != watchedBefore)
        {
            rule.LastCheckedUtc = null;
        }
    }
}
