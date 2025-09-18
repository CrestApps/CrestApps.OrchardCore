using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityDisplayDriver : DisplayDriver<OmnichannelActivity>
{
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly ICatalog<OmnichannelCampaign> _campaignsCatalog;

    internal readonly IStringLocalizer S;

    public OmnichannelActivityDisplayDriver(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        ICatalog<OmnichannelCampaign> campaignsCatalog,
        IStringLocalizer<OmnichannelActivityDisplayDriver> stringLocalizer)
    {
        _dispositionsCatalog = dispositionsCatalog;
        _campaignsCatalog = campaignsCatalog;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelActivity activity, BuildEditorContext context)
    {
        return Initialize<OmnichannelActivityViewModel>("OmnichannelActivityFields_Edit", async model =>
        {
            var campaign = await _campaignsCatalog.FindByIdAsync(activity.CampaignId);

            var campaignDispositionIds = campaign?.DispositionIds ?? [];

            if (!string.IsNullOrEmpty(activity.DispositionId) && !campaignDispositionIds.Contains(activity.DispositionId))
            {
                campaignDispositionIds.Add(activity.DispositionId);
            }
            model.Notes = activity.Notes;
            model.DispositionId = activity.DispositionId;
            model.Dispositions = await _dispositionsCatalog.GetAsync(campaignDispositionIds);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivity activity, UpdateEditorContext context)
    {
        var model = new OmnichannelActivityViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.DispositionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DispositionId), S["The Disposition field is required."]);
        }
        else
        {
            var campaign = await _campaignsCatalog.FindByIdAsync(activity.CampaignId);

            var campaignDispositionIds = campaign?.DispositionIds ?? [];

            if (!string.IsNullOrEmpty(activity.DispositionId))
            {
                campaignDispositionIds.Add(activity.DispositionId);
            }

            var dispositions = await _dispositionsCatalog.GetAsync(campaignDispositionIds);

            var disposition = dispositions.FirstOrDefault(d => d.Id == model.DispositionId);

            if (disposition == null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DispositionId), S["The selected Disposition is invalid."]);
            }
            else if (disposition.CaptureDate && !model.ScheduleDate.HasValue)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ScheduleDate), S["The Schedule Date field is required."]);
            }
        }

        activity.DispositionId = model.DispositionId;
        activity.Notes = model.Notes;

        activity.Put(new DispositionMetadata
        {
            ScheduledDate = model.ScheduleDate,
        });

        return Edit(activity, context);
    }
}
