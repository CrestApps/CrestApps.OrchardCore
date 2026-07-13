using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelCampaignDisplayDriver : DisplayDriver<OmnichannelCampaign>
{
    private readonly ICatalogManager<OmnichannelCampaignGroup> _campaignGroupManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignDisplayDriver"/> class.
    /// </summary>
    /// <param name="campaignGroupManager">The campaign group manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelCampaignDisplayDriver(
        ICatalogManager<OmnichannelCampaignGroup> campaignGroupManager,
        IStringLocalizer<OmnichannelCampaignDisplayDriver> stringLocalizer)
    {
        _campaignGroupManager = campaignGroupManager;
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
            model.CampaignGroupId = campaign.CampaignGroupId;
            model.CampaignGroups = (await _campaignGroupManager.GetAllAsync())
                .OrderBy(group => group.DisplayText)
                .Select(group => new SelectListItem(group.DisplayText ?? group.ItemId, group.ItemId))
                .ToList();
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

        campaign.DisplayText = model.DisplayText?.Trim();
        campaign.Description = model.Description?.Trim();
        campaign.CampaignGroupId = model.CampaignGroupId;

        return Edit(campaign, context);
    }
}
