using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Edits the Check AI Conversation Metric task.
/// </summary>
public sealed class CheckConversationMetricTaskDisplayDriver : ActivityDisplayDriver<CheckConversationMetricTask, CheckConversationMetricTaskViewModel>
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckConversationMetricTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckConversationMetricTaskDisplayDriver(IAIProfileStore profileStore, IStringLocalizer<CheckConversationMetricTaskDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(CheckConversationMetricTask activity, CheckConversationMetricTaskViewModel model)
    {

        model.Metric = activity.Metric;
        model.ProfileId = activity.ProfileId;
        model.WindowMinutes = activity.WindowMinutes;
        model.Condition = activity.Condition;
        model.Threshold = activity.Threshold;
        model.MinimumSampleSize = activity.MinimumSampleSize;
        model.Profiles = await GetProfilesAsync();
        model.Metrics =
        [
            new SelectListItem(S["Conversations started"], nameof(AIConversationMetric.Conversations)),
            new SelectListItem(S["Resolution rate (%)"], nameof(AIConversationMetric.ResolutionRate)),
            new SelectListItem(S["Average conversation length (seconds)"], nameof(AIConversationMetric.AverageHandleTimeSeconds)),
            new SelectListItem(S["Average response time (ms)"], nameof(AIConversationMetric.AverageResponseLatencyMs)),
            new SelectListItem(S["Rated down (%)"], nameof(AIConversationMetric.NegativeFeedbackRate)),
            new SelectListItem(S["Average QA score"], nameof(AIConversationMetric.AverageQAScore)),
            new SelectListItem(S["QA failures (%)"], nameof(AIConversationMetric.QAFailureRate)),
            new SelectListItem(S["Tool failures (%)"], nameof(AIConversationMetric.ToolFailureRate)),
            new SelectListItem(S["Unsupported answers (%)"], nameof(AIConversationMetric.HallucinationRate)),
            new SelectListItem(S["AI calls"], nameof(AIConversationMetric.AICalls)),
            new SelectListItem(S["AI calls handed to a person (%)"], nameof(AIConversationMetric.AICallHandoffRate)),
            new SelectListItem(S["AI calls that failed (%)"], nameof(AIConversationMetric.AICallFailureRate)),
            new SelectListItem(S["Average time to the AI's first words (ms)"], nameof(AIConversationMetric.AverageTimeToFirstAudioMs)),
        ];
        model.Conditions =
        [
            new SelectListItem(S["Is above the threshold"], nameof(AIConversationAlertCondition.Above)),
            new SelectListItem(S["Is below the threshold"], nameof(AIConversationAlertCondition.Below)),
            new SelectListItem(S["Rises by more than the threshold, in % of the window before"], nameof(AIConversationAlertCondition.RisesByPercent)),
            new SelectListItem(S["Falls by more than the threshold, in % of the window before"], nameof(AIConversationAlertCondition.FallsByPercent)),
        ];
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(CheckConversationMetricTask activity, UpdateEditorContext context)
    {
        var model = new CheckConversationMetricTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);


        if (model.WindowMinutes < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.WindowMinutes), S["The window is at least one minute."]);
        }

        if (model.MinimumSampleSize < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumSampleSize), S["The fewest samples cannot be negative."]);
        }

        activity.Metric = model.Metric;
        activity.ProfileId = model.ProfileId;
        activity.WindowMinutes = model.WindowMinutes;
        activity.Condition = model.Condition;
        activity.Threshold = model.Threshold;
        activity.MinimumSampleSize = model.MinimumSampleSize;

        return Edit(activity, context);
    }

    private async Task<IEnumerable<SelectListItem>> GetProfilesAsync()
        => (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
}
