using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using OrchardCore.Documents;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Measures an AI conversation metric over the last window and compares it with a threshold. Started by a timer,
/// it opens an alert the first time the threshold is crossed and resolves it when the metric is back, so the
/// workflow notifies once at each change.
/// </summary>
public sealed class CheckConversationMetricTask : TaskActivity<CheckConversationMetricTask>
{
    private readonly AIConversationMetrics _metrics;
    private readonly IDocumentManager<AIConversationAlertState> _stateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConversationMetricTask"/> class.
    /// </summary>
    /// <param name="metrics">The service that measures the metric.</param>
    /// <param name="stateManager">The document the open alerts are kept in.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckConversationMetricTask(
        AIConversationMetrics metrics,
        IDocumentManager<AIConversationAlertState> stateManager,
        IClock clock,
        IStringLocalizer<CheckConversationMetricTask> stringLocalizer)
    {
        _metrics = metrics;
        _stateManager = stateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Check AI Conversation Metric"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the metric to measure.
    /// </summary>
    public AIConversationMetric Metric
    {
        get => GetProperty(() => AIConversationMetric.QAFailureRate);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the AI profile to measure, or empty for all of them.
    /// </summary>
    public string ProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the length of the window, in minutes.
    /// </summary>
    public int WindowMinutes
    {
        get => GetProperty(() => 60);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how the value is compared with the threshold.
    /// </summary>
    public AIConversationAlertCondition Condition
    {
        get => GetProperty(() => AIConversationAlertCondition.Above);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the threshold.
    /// </summary>
    public double Threshold
    {
        get => GetProperty<double>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the fewest conversations, evaluations or calls the metric must be measured over before it can
    /// alert, so two calls in a quiet hour do not raise a 50% failure rate.
    /// </summary>
    public int MinimumSampleSize
    {
        get => GetProperty(() => 10);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Alert started"]), new Outcome(S["Still alerting"]), new Outcome(S["Resolved"]), new Outcome(S["Normal"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var utcNow = _clock.UtcNow;
        var window = TimeSpan.FromMinutes(Math.Max(1, WindowMinutes));
        var profileId = string.IsNullOrWhiteSpace(ProfileId) ? null : ProfileId;

        var current = await _metrics.MeasureAsync(Metric, profileId, utcNow - window, utcNow);
        AIConversationMetricValue previous = null;

        if (Condition is AIConversationAlertCondition.RisesByPercent or AIConversationAlertCondition.FallsByPercent)
        {
            previous = await _metrics.MeasureAsync(Metric, profileId, utcNow - window - window, utcNow - window);
        }

        var crossed = IsCrossed(Condition, Threshold, current, previous, MinimumSampleSize);
        var key = $"{workflowContext.WorkflowType?.WorkflowTypeId}:{activityContext.ActivityRecord?.ActivityId}";

        var state = await _stateManager.GetOrCreateMutableAsync();
        var wasOpen = state.Open.TryGetValue(key, out var open);

        string outcome;

        if (crossed)
        {
            outcome = wasOpen ? "Still alerting" : "Alert started";

            if (!wasOpen)
            {
                open = new AIConversationOpenAlert { SinceUtc = utcNow, Value = current.Value ?? 0 };
                state.Open[key] = open;
                await _stateManager.UpdateAsync(state);
            }
        }
        else if (wasOpen && current.SampleSize >= Math.Max(0, MinimumSampleSize))
        {
            // Too few samples says nothing either way, so an open alert only resolves on a measured window.
            outcome = "Resolved";
            state.Open.Remove(key);
            await _stateManager.UpdateAsync(state);
        }
        else
        {
            outcome = wasOpen ? "Still alerting" : "Normal";
        }

        var result = new Dictionary<string, object>
        {
            ["Metric"] = Metric.ToString(),
            ["Value"] = current.Value,
            ["PreviousValue"] = previous?.Value,
            ["SampleSize"] = current.SampleSize,
            ["Threshold"] = Threshold,
            ["Condition"] = Condition.ToString(),
            ["WindowMinutes"] = (int)window.TotalMinutes,
            ["ProfileId"] = profileId,
            ["SinceUtc"] = open?.SinceUtc,
        };

        workflowContext.Output["Alert"] = result;
        workflowContext.LastResult = result;

        return Outcome(outcome);
    }

    /// <summary>
    /// Whether the measured value crosses the threshold. A window measured over fewer samples than the minimum
    /// never crosses, and neither does a change from a window with no value.
    /// </summary>
    /// <param name="condition">How the value is compared.</param>
    /// <param name="threshold">The threshold.</param>
    /// <param name="current">The value over the last window.</param>
    /// <param name="previous">The value over the window before, for a change.</param>
    /// <param name="minimumSampleSize">The fewest samples that can alert.</param>
    internal static bool IsCrossed(
        AIConversationAlertCondition condition,
        double threshold,
        AIConversationMetricValue current,
        AIConversationMetricValue previous,
        int minimumSampleSize)
        => AIConversationAlerts.IsCrossed(condition, threshold, current, previous, minimumSampleSize);
}
