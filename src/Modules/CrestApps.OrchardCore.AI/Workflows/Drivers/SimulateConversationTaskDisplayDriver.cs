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
/// Edits the Simulate AI Conversation task.
/// </summary>
public sealed class SimulateConversationTaskDisplayDriver : ActivityDisplayDriver<SimulateConversationTask, SimulateConversationTaskViewModel>
{
    private readonly IAIProfileStore _profileStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulateConversationTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the templates.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SimulateConversationTaskDisplayDriver(IAIProfileStore profileStore, ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<SimulateConversationTaskDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(SimulateConversationTask activity, SimulateConversationTaskViewModel model)
    {

        model.ProfileId = activity.ProfileId;
        model.Scenario = activity.Scenario;
        model.Variables = activity.Variables;
        model.Criteria = activity.Criteria;
        model.MaxTurns = activity.MaxTurns;
        model.PassingScore = activity.PassingScore;
        model.Label = activity.Label;
        model.Profiles = await GetProfilesAsync();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SimulateConversationTask activity, UpdateEditorContext context)
    {
        var model = new SimulateConversationTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);


        if (string.IsNullOrWhiteSpace(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["Choose the AI profile to test."]);
        }

        if (string.IsNullOrWhiteSpace(model.Scenario))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Scenario), S["Describe the customer the test plays."]);
        }

        if (model.MaxTurns is < 1 or > 50)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxTurns), S["The most customer turns is from 1 to 50."]);
        }

        if (model.PassingScore is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PassingScore), S["The passing score is from 0 to 100."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Scenario) && !_liquidTemplateManager.Validate(model.Scenario, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Scenario), S["The customer is not a valid Liquid template."]);
        }
        if (!string.IsNullOrWhiteSpace(model.Criteria) && !_liquidTemplateManager.Validate(model.Criteria, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Criteria), S["The criteria is not a valid Liquid template."]);
        }
        if (!string.IsNullOrWhiteSpace(model.Variables) && !_liquidTemplateManager.Validate(model.Variables, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Variables), S["The variables is not a valid Liquid template."]);
        }

        activity.ProfileId = model.ProfileId;
        activity.Scenario = model.Scenario;
        activity.Variables = model.Variables;
        activity.Criteria = model.Criteria;
        activity.MaxTurns = model.MaxTurns;
        activity.PassingScore = model.PassingScore;
        activity.Label = model.Label?.Trim();

        return Edit(activity, context);
    }

    private async Task<IEnumerable<SelectListItem>> GetProfilesAsync()
        => (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
}
