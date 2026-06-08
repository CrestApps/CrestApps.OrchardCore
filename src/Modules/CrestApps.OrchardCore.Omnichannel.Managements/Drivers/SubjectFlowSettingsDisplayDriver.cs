using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class SubjectFlowSettingsDisplayDriver : DisplayDriver<SubjectFlowSettings>
{
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointsCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFlowSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="channelEndpointsCatalog">The channel endpoints catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubjectFlowSettingsDisplayDriver(
        ICatalog<OmnichannelCampaign> campaignCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointsCatalog,
        IStringLocalizer<SubjectFlowSettingsDisplayDriver> stringLocalizer)
    {
        _campaignCatalog = campaignCatalog;
        _channelEndpointsCatalog = channelEndpointsCatalog;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectFlowSettings flowSettings, BuildEditorContext context)
    {
        return Initialize<SubjectFlowSettingsViewModel>("SubjectFlowSettingsFields_Edit", async model =>
        {
            model.CampaignId = flowSettings.CampaignId;
            model.InteractionType = flowSettings.InteractionType;
            model.Channel = flowSettings.Channel;
            model.ChannelEndpointId = flowSettings.ChannelEndpointId;

            model.Campaigns = (await _campaignCatalog.GetAllAsync())
                .Select(c => new SelectListItem(c.DisplayText, c.ItemId))
                .OrderBy(x => x.Text);

            model.Channels =
            [
                new(S["Phone"], OmnichannelConstants.Channels.Phone),
                new(S["SMS"], OmnichannelConstants.Channels.Sms),
                new(S["Email"], OmnichannelConstants.Channels.Email),
            ];

            model.ChannelEndpoints = (await _channelEndpointsCatalog.GetAllAsync())
                .Select(x => new SelectListItem(x.DisplayText, x.ItemId))
                .OrderBy(x => x.Text);

            model.InteractionTypes =
            [
                new(S["Manual"], nameof(ActivityInteractionType.Manual)),
                new(S["Automated"], nameof(ActivityInteractionType.Automated)),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectFlowSettings flowSettings, UpdateEditorContext context)
    {
        var model = new SubjectFlowSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.CampaignId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignId), S["Campaign is a required field."]);
        }

        if (string.IsNullOrEmpty(model.Channel))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["Channel is a required field."]);
        }

        if (model.InteractionType == ActivityInteractionType.Automated &&
            string.IsNullOrEmpty(model.ChannelEndpointId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChannelEndpointId), S["Channel endpoint is required for automated interactions."]);
        }

        flowSettings.CampaignId = model.CampaignId;
        flowSettings.InteractionType = model.InteractionType;
        flowSettings.Channel = model.Channel;
        flowSettings.ChannelEndpointId = model.ChannelEndpointId;

        return Edit(flowSettings, context);
    }
}
