using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelCampaignDisplayDriver : DisplayDriver<OmnichannelCampaign>
{
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;

    private readonly IStringLocalizer S;

    public OmnichannelCampaignDisplayDriver(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IStringLocalizer<OmnichannelCampaignDisplayDriver> stringLocalizer)
    {
        _dispositionsCatalog = dispositionsCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelCampaign campaign, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelCampaign_Fields_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("OmnichannelCampaign_Buttons_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("OmnichannelCampaign_DefaultMeta_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelCampaign campaign, BuildEditorContext context)
    {
        return Initialize<OmnichannelCampaignViewModel>("OmnichannelCampaignFields_Edit", async model =>
        {
            model.DisplayText = campaign.DisplayText;
            model.Description = campaign.Description;
            var dispositions = await _dispositionsCatalog.GetAllAsync();

            model.Dispositions = dispositions.Select(d => new SelectListItem
            {
                Text = d.DisplayText,
                Value = d.Id,
                Selected = campaign.DispositionIds is not null && campaign.DispositionIds.Contains(d.Id)
            }).OrderBy(x => x.Text)
            .ToArray();
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelCampaign campaign, UpdateEditorContext context)
    {
        var model = new OmnichannelCampaignViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Name is a required field."]);
        }

        var dispositions = await _dispositionsCatalog.GetAllAsync();

        var selectedDispositionIds = (model.Dispositions?.Where(x => x.Selected)
            .Select(d => d.Value).ToList() ?? [])
            .Intersect(dispositions.Select(y => y.Id))
            .ToArray();

        if (selectedDispositionIds is null || selectedDispositionIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Dispositions), S["At least one Disposition must be selected."]);
        }
        else
        {
            campaign.DispositionIds = selectedDispositionIds;
        }

        campaign.DisplayText = model.DisplayText?.Trim();
        campaign.Description = model.Description?.Trim();

        return Edit(campaign, context);
    }
}
