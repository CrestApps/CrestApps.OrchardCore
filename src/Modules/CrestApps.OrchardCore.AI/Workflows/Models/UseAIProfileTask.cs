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
    private readonly AIProfileVersionCopies _versionCopies;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseAIProfileTask"/> class.
    /// </summary>
    /// <param name="versionCopies">Runs an earlier version of a profile.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UseAIProfileTask(AIProfileVersionCopies versionCopies, IStringLocalizer<UseAIProfileTask> stringLocalizer)
    {
        _versionCopies = versionCopies;
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
    /// <summary>
    /// Gets or sets the version of the profile to answer with, or <see langword="null"/> for its current version. An
    /// earlier version runs from a copy made on first use, so two versions of one profile can be split live.
    /// </summary>
    public int? ProfileVersion
    {
        get => GetProperty<int?>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Not starting"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (string.IsNullOrWhiteSpace(ProfileId) ||
            !workflowContext.Input.TryGetValue(AIConversationWorkflowInput.StartingResult, out var value) ||
            value is not AIConversationStartingResult starting)
        {
            return Outcome("Not starting");
        }

        if (ProfileVersion is not > 0)
        {
            starting.ProfileId = ProfileId;

            return Outcome("Done");
        }

        // An earlier version answers through its copy; a version that no longer exists leaves the profile as it was.
        var profile = await _versionCopies.GetOrCreateAsync(ProfileId, ProfileVersion.Value);

        if (profile is null)
        {
            return Outcome("Not starting");
        }

        starting.ProfileId = profile.ItemId;

        return Outcome("Done");
    }
}
