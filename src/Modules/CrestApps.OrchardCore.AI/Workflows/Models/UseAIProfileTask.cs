using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// In an <c>AI Conversation Starting</c> workflow, answers the conversation with a different AI profile: route a
/// caller to the right assistant, or split traffic between two profiles.
/// </summary>
public sealed class UseAIProfileTask : TaskActivity<UseAIProfileTask>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseAIProfileTask"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UseAIProfileTask(IStringLocalizer<UseAIProfileTask> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Use AI Profile"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the AI profile to answer with.
    /// </summary>
    public string ProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Not starting"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (string.IsNullOrWhiteSpace(ProfileId) ||
            !workflowContext.Input.TryGetValue(AIConversationWorkflowInput.StartingResult, out var value) ||
            value is not AIConversationStartingResult starting)
        {
            return Outcome("Not starting");
        }

        starting.ProfileId = ProfileId;

        return Outcome("Done");
    }
}
