using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class TryAgainCampaignActionDisplayDriver : DisplayDriver<CampaignAction>
{
    public override IDisplayResult Edit(CampaignAction action, BuildEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<TryAgainCampaignActionViewModel>("TryAgainCampaignActionFields_Edit", model =>
        {
            if (action.TryGet<TryAgainActionMetadata>(out var metadata))
            {
                model.MaxAttempt = metadata.MaxAttempt;
                model.UrgencyLevel = metadata.UrgencyLevel;
                model.NormalizedUserName = metadata.NormalizedUserName;
                model.DefaultScheduleHours = metadata.DefaultScheduleHours;
            }
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(CampaignAction action, UpdateEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new TryAgainCampaignActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        action.Put(new TryAgainActionMetadata
        {
            MaxAttempt = model.MaxAttempt,
            UrgencyLevel = model.UrgencyLevel,
            NormalizedUserName = model.NormalizedUserName?.Trim(),
            DefaultScheduleHours = model.DefaultScheduleHours,
        });

        return Edit(action, context);
    }
}
