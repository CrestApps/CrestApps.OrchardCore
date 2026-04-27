using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Represents the AI chat session all fields extracted event.
/// </summary>
public sealed class AIChatSessionAllFieldsExtractedEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionAllFieldsExtractedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionAllFieldsExtractedEvent(
        IStringLocalizer<AIChatSessionAllFieldsExtractedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string Name => nameof(AIChatSessionAllFieldsExtractedEvent);

    public override LocalizedString DisplayText => S["AI Chat Session All Fields Extracted"];

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
