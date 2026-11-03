using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// What a check of an alert rule changed.
/// </summary>
public enum AIAlertChange
{
    /// <summary>
    /// Nothing: the rule was not due, or stayed as it was.
    /// </summary>
    None,

    /// <summary>
    /// The threshold was crossed and an incident opened.
    /// </summary>
    Triggered,

    /// <summary>
    /// The metric is back and the incident resolved.
    /// </summary>
    Resolved,
}

/// <summary>
/// Checks alert rules against the same measurements the reports use, and opens and resolves their incidents.
/// </summary>
public sealed class AIAlertRuleEvaluator
{
    private readonly AIConversationMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIAlertRuleEvaluator"/> class.
    /// </summary>
    /// <param name="metrics">Measures the metrics.</param>
    public AIAlertRuleEvaluator(AIConversationMetrics metrics)
    {
        _metrics = metrics;
    }

    /// <summary>
    /// Whether a rule is due for a check.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when it is enabled and its interval has passed.</returns>
    public static bool IsDue(AIAlertRule rule, DateTime nowUtc)
        => rule is not null &&
            rule.Enabled &&
            (rule.LastCheckedUtc is not { } last || nowUtc - last >= TimeSpan.FromMinutes(Math.Max(1, rule.CheckEveryMinutes)));

    /// <summary>
    /// Checks a rule and records the outcome on it.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What changed.</returns>
    public async Task<AIAlertChange> CheckAsync(AIAlertRule rule, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var window = TimeSpan.FromMinutes(Math.Max(1, rule.WindowMinutes));
        var profileId = string.IsNullOrWhiteSpace(rule.ProfileId) ? null : rule.ProfileId;
        var current = await _metrics.MeasureAsync(rule.Metric, profileId, nowUtc - window, nowUtc, cancellationToken);
        AIConversationMetricValue previous = null;

        if (rule.Condition is AIConversationAlertCondition.RisesByPercent or AIConversationAlertCondition.FallsByPercent)
        {
            previous = await _metrics.MeasureAsync(rule.Metric, profileId, nowUtc - window - window, nowUtc - window, cancellationToken);
        }

        return Apply(rule, current, previous, nowUtc);
    }

    /// <summary>
    /// Records a measurement on a rule: opens an incident when the threshold is crossed, resolves an open one when a
    /// measured window is back within it, and keeps the worst value of an open incident.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="current">The latest window.</param>
    /// <param name="previous">The window before it, for a change in percent.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>What changed.</returns>
    public static AIAlertChange Apply(AIAlertRule rule, AIConversationMetricValue current, AIConversationMetricValue previous, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);

        rule.LastCheckedUtc = nowUtc;
        rule.LastValue = current?.Value;
        rule.LastSampleSize = current?.SampleSize ?? 0;

        var crossed = AIConversationAlerts.IsCrossed(rule.Condition, rule.Threshold, current, previous, rule.MinimumSampleSize);
        var open = rule.Incidents.FirstOrDefault(incident => incident.ResolvedUtc is null);

        if (crossed)
        {
            var value = current.Value ?? 0;

            if (rule.IsOpen && open is not null)
            {
                open.PeakValue = rule.Condition is AIConversationAlertCondition.Below or AIConversationAlertCondition.FallsByPercent
                    ? Math.Min(open.PeakValue, value)
                    : Math.Max(open.PeakValue, value);

                return AIAlertChange.None;
            }

            rule.IsOpen = true;
            rule.Incidents.Insert(0, new AIAlertIncident { OpenedUtc = nowUtc, OpenedValue = value, PeakValue = value });

            if (rule.Incidents.Count > AIAlertRule.MaxIncidents)
            {
                rule.Incidents.RemoveRange(AIAlertRule.MaxIncidents, rule.Incidents.Count - AIAlertRule.MaxIncidents);
            }

            return AIAlertChange.Triggered;
        }

        // Too few samples says nothing either way, so an open incident resolves only on a measured window.
        if (rule.IsOpen && (current?.SampleSize ?? 0) >= Math.Max(0, rule.MinimumSampleSize))
        {
            rule.IsOpen = false;

            if (open is not null)
            {
                open.ResolvedUtc = nowUtc;
            }

            return AIAlertChange.Resolved;
        }

        return AIAlertChange.None;
    }
}
