using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Represents the AI chat session closed event.
/// </summary>
public sealed class AIChatSessionClosedEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionClosedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionClosedEvent(
        IStringLocalizer<AIChatSessionClosedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string Name => nameof(AIChatSessionClosedEvent);

    public override LocalizedString DisplayText => S["AI Chat Session Closed"];

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
