using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelSubjectPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<OmnichannelSubjectPart>
{
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointsCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectPartSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="channelEndpointsCatalog">The channel endpoints catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelSubjectPartSettingsDisplayDriver(
        ICatalog<OmnichannelCampaign> campaignCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointsCatalog,
        IStringLocalizer<OmnichannelSubjectPartSettingsDisplayDriver> stringLocalizer)
    {
        _campaignCatalog = campaignCatalog;
        _channelEndpointsCatalog = channelEndpointsCatalog;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<OmnichannelSubjectPartSettingsViewModel>("OmnichannelSubjectPartSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<OmnichannelSubjectPartSettings>();

            model.Direction = settings.Direction;
            model.InteractionType = settings.InteractionType;
            model.Channel = settings.Channel;
            model.ChannelEndpointId = settings.ChannelEndpointId;
            model.DefaultCampaignId = settings.DefaultCampaignId;
            model.RequireDisposition = settings.RequireDisposition;

            model.Directions =
            [
                new(S["Outbound"], nameof(SubjectDirection.Outbound)),
                new(S["Inbound"], nameof(SubjectDirection.Inbound)),
            ];

            model.InteractionTypes =
            [
                new(S["Manual"], nameof(ActivityInteractionType.Manual)),
                new(S["Automated"], nameof(ActivityInteractionType.Automated)),
            ];

            model.Channels =
            [
                new(S["Phone"], OmnichannelConstants.Channels.Phone),
                new(S["SMS"], OmnichannelConstants.Channels.Sms),
            ];

            model.ChannelEndpoints = (await _channelEndpointsCatalog.GetAllAsync())
                .Select(endpoint => new SelectListItem(endpoint.DisplayText, endpoint.ItemId))
                .OrderBy(item => item.Text);

            model.Campaigns = (await _campaignCatalog.GetAllAsync())
                .Select(campaign => new SelectListItem(campaign.DisplayText, campaign.ItemId))
                .OrderBy(item => item.Text);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new OmnichannelSubjectPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Direction == SubjectDirection.Inbound)
        {
            if (string.IsNullOrWhiteSpace(model.Channel))
            {
                context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.Channel)}", S["A channel is required for inbound subjects."]);
            }

            if (model.InteractionType == ActivityInteractionType.Automated && string.IsNullOrWhiteSpace(model.ChannelEndpointId))
            {
                context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.ChannelEndpointId)}", S["A channel endpoint is required for automated inbound subjects."]);
            }
        }

        context.Builder.WithSettings(new OmnichannelSubjectPartSettings
        {
            Direction = model.Direction,
            InteractionType = model.Direction == SubjectDirection.Inbound ? model.InteractionType : ActivityInteractionType.Manual,
            Channel = model.Direction == SubjectDirection.Inbound ? model.Channel : null,
            ChannelEndpointId = model.Direction == SubjectDirection.Inbound ? model.ChannelEndpointId : null,
            DefaultCampaignId = model.DefaultCampaignId,
            RequireDisposition = model.RequireDisposition,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
