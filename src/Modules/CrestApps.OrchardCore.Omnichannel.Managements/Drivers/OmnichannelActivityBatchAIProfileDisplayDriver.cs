using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityBatchAIProfileDisplayDriver : DisplayDriver<OmnichannelActivityBatch>
{
    private readonly IAIProfileManager _profileManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IStringLocalizer S;

    public OmnichannelActivityBatchAIProfileDisplayDriver(
        IAIProfileManager profileManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IStringLocalizer<OmnichannelActivityBatchAIProfileDisplayDriver> stringLocalizer)
    {
        _profileManager = profileManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelActivityBatch batch, BuildEditorContext context)
    {
        if (!string.Equals(batch.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<OmnichannelActivityBatchAIProfileViewModel>("OmnichannelActivityBatchAIProfileFields_Edit", async model =>
        {
            model.Source = batch.Source;
            model.AIProfileId = batch.AIProfileId;

            var selectedProfileId = model.AIProfileId;

            if (string.IsNullOrWhiteSpace(selectedProfileId) &&
                !string.IsNullOrWhiteSpace(batch.SubjectContentType))
            {
                var flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(batch.SubjectContentType);
                selectedProfileId = flowSettings?.ProfileId;
            }

            model.AIProfiles = await GetAIProfileOptionsAsync(selectedProfileId);
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivityBatch batch, UpdateEditorContext context)
    {
        if (!string.Equals(batch.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase))
        {
            batch.AIProfileId = null;

            return null;
        }

        var model = new OmnichannelActivityBatchAIProfileViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var flowSettings = !string.IsNullOrWhiteSpace(batch.SubjectContentType)
            ? await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(batch.SubjectContentType)
            : null;
        var selectedProfileId = string.IsNullOrWhiteSpace(model.AIProfileId)
            ? flowSettings?.ProfileId
            : model.AIProfileId.Trim();

        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["AI profile is required for automatic inventory loads."]);
        }
        else
        {
            var profile = await _profileManager.FindByIdAsync(selectedProfileId);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["The selected AI profile is invalid."]);
            }
            else if (!HasInitialPrompt(profile))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["The selected AI profile must have Add initial prompt enabled."]);
            }
        }

        batch.AIProfileId = model.AIProfileId?.Trim();

        return Edit(batch, context);
    }

    private async Task<IEnumerable<SelectListItem>> GetAIProfileOptionsAsync(string selectedProfileId)
    {
        var chatProfiles = await _profileManager.GetAsync(AIProfileType.Chat);

        return chatProfiles
            .Where(HasInitialPrompt)
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId)
            {
                Selected = string.Equals(profile.ItemId, selectedProfileId, StringComparison.OrdinalIgnoreCase),
            });
    }

    private static bool HasInitialPrompt(AIProfile profile)
    {
        var metadata = profile.GetOrCreate<AIProfileMetadata>();

        return !string.IsNullOrWhiteSpace(metadata.InitialPrompt);
    }
}
