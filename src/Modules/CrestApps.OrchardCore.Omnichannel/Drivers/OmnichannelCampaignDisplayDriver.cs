using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Drivers;

internal sealed class OmnichannelCampaignDisplayDriver : DisplayDriver<OmnichannelCampaign>
{
    private readonly IStringLocalizer S;

    public OmnichannelCampaignDisplayDriver(
        IStringLocalizer<OmnichannelCampaignDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelCampaign campaign, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelCampaign_Fields_SummaryAdmin", campaign).Location("Content:1"),
            View("OmnichannelCampaign_Buttons_SummaryAdmin", campaign).Location("Actions:5"),
            View("OmnichannelCampaign_DefaultMeta_SummaryAdmin", campaign).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelCampaign campaign, BuildEditorContext context)
    {
        return Initialize<OmnichannelCampaignViewModel>("OmnichannelCampaignFields_Edit", model =>
        {
            model.DisplayText = campaign.DisplayText;
            model.Descriptions = campaign.Descriptions;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelCampaign campaign, UpdateEditorContext context)
    {
        var model = new OmnichannelCampaignViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        model.DisplayText = model.DisplayText.Trim();

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Name cannot be empty."]);
        }

        campaign.DisplayText = model.DisplayText;
        campaign.Descriptions = model.Descriptions?.Trim();

        return Edit(campaign, context);
    }
}
