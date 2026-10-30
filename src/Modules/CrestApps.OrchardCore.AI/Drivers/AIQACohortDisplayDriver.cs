using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Displays a QA cohort in the admin list and edits it.
/// </summary>
public sealed class AIQACohortDisplayDriver : DisplayDriver<AIQACohort>
{
    private readonly IAIProfileStore _profileStore;
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQACohortDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIQACohortDisplayDriver(
        IAIProfileStore profileStore,
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<AIQACohortDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AIQACohort cohort, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("AIQACohort_Fields_SummaryAdmin", cohort)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIQACohort_DefaultTags_SummaryAdmin", cohort)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Sample:5"),
            View("AIQACohort_Buttons_SummaryAdmin", cohort).Location("Actions:5"),
            View("AIQACohort_ActionsMenu_SummaryAdmin", cohort).Location("ActionsMenu:10"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIQACohort cohort, BuildEditorContext context)
    {
        return Initialize<AIQACohortViewModel>("AIQACohortFields_Edit", async model =>
        {
            model.Name = cohort.Name;
            model.Enabled = cohort.Enabled;
            model.ProfileIds = cohort.ProfileIds?.ToArray() ?? [];
            model.SamplePercent = cohort.SamplePercent;
            model.Criteria = cohort.Criteria;
            model.PassingScore = cohort.PassingScore;
            model.CheckKnowledge = cohort.CheckKnowledge;
            model.JudgeDeploymentName = cohort.JudgeDeploymentName;
            model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
                .Where(profile => !profile.Has<AIProfileVersionCopy>())
                .OrderBy(profile => profile.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId, model.ProfileIds.Contains(profile.ItemId)))
                .ToList();
            model.Deployments = (await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Chat))
                .Where(deployment => !string.IsNullOrWhiteSpace(deployment.Name))
                .OrderBy(deployment => deployment.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(deployment => new SelectListItem(deployment.Name, deployment.Name))
                .ToList();
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIQACohort cohort, UpdateEditorContext context)
    {
        var model = new AIQACohortViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The name is required."]);
        }

        if (model.ProfileIds is not { Length: > 0 })
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileIds), S["Choose at least one AI profile."]);
        }

        if (model.SamplePercent is < 1 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SamplePercent), S["The sample must be between 1 and 100 percent."]);
        }

        if (model.PassingScore is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PassingScore), S["The passing score must be between 0 and 100."]);
        }

        cohort.Name = model.Name?.Trim();
        cohort.Enabled = model.Enabled;
        cohort.ProfileIds = model.ProfileIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? [];
        cohort.SamplePercent = model.SamplePercent;
        cohort.Criteria = model.Criteria?.Trim();
        cohort.PassingScore = model.PassingScore;
        cohort.CheckKnowledge = model.CheckKnowledge;
        cohort.JudgeDeploymentName = string.IsNullOrWhiteSpace(model.JudgeDeploymentName) ? null : model.JudgeDeploymentName;

        return Edit(cohort, context);
    }
}
