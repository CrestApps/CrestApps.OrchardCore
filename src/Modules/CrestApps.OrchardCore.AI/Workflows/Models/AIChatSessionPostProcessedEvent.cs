using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Represents the AI chat session post processed event.
/// </summary>
public sealed class AIChatSessionPostProcessedEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionPostProcessedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionPostProcessedEvent(
        IStringLocalizer<AIChatSessionPostProcessedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string Name => nameof(AIChatSessionPostProcessedEvent);

    public override LocalizedString DisplayText => S["AI Chat Session Post-Processed"];

    public override LocalizedString Category => S["AI Chat"];

    public string ProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override ValueTask<IEnumerable<Outcome>> GetPossibleOutcomesAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        return new ValueTask<IEnumerable<Outcome>>(
        [
            Outcome(S["Done"]),
            ]);
    }

    public override ActivityExecutionResult Resume(
    WorkflowExecutionContext workflowContext,
    ActivityContext activityContext)
    {
        return Outcomes("Done");
    }

    public override ActivityExecutionResult Execute(
    WorkflowExecutionContext workflowContext,
    ActivityContext activityContext)
    {
        return Outcomes("Done");
    }
}
