using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelCampaignGroupDisplayDriver : DisplayDriver<OmnichannelCampaignGroup>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelCampaignGroupDisplayDriver(IStringLocalizer<OmnichannelCampaignGroupDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelCampaignGroup group, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelCampaignGroup_Fields_SummaryAdmin", group)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("OmnichannelCampaignGroup_Buttons_SummaryAdmin", group)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("OmnichannelCampaignGroup_DefaultMeta_SummaryAdmin", group)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5"));
    }

    public override IDisplayResult Edit(OmnichannelCampaignGroup group, BuildEditorContext context)
    {
        return Initialize<OmnichannelCampaignGroupViewModel>("OmnichannelCampaignGroupFields_Edit", model =>
        {
            model.DisplayText = group.DisplayText;
            model.Description = group.Description;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelCampaignGroup group, UpdateEditorContext context)
    {
        var model = new OmnichannelCampaignGroupViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Name is a required field."]);
        }

        group.DisplayText = model.DisplayText?.Trim();
        group.Description = model.Description?.Trim();

        return Edit(group, context);
    }
}
