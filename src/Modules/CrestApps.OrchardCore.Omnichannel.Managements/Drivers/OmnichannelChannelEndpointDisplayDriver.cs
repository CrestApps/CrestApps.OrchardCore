using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelChannelEndpointDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelChannelEndpointDisplayDriver(IStringLocalizer<OmnichannelChannelEndpointDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelChannelEndpoint endpoint, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelChannelEndpoint_Fields_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("OmnichannelChannelEndpoint_Buttons_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("OmnichannelChannelEndpoint_DefaultMeta_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        // The channel is the source, fixed at creation, so it is not editable here. Channel-specific fields
        // (provider, routing, ...) are contributed by the display drivers that target that channel.
        return Initialize<OmnichannelChannelEndpointViewModel>("OmnichannelChannelEndpointFields_Edit", model =>
        {
            model.DisplayText = endpoint.DisplayText;
            model.Description = endpoint.Description;
            model.Channel = endpoint.Channel;
            model.Value = endpoint.Value;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        var model = new OmnichannelChannelEndpointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        endpoint.DisplayText = model.DisplayText?.Trim();
        endpoint.Description = model.Description?.Trim();
        endpoint.Value = model.Value?.Trim();

        return Edit(endpoint, context);
    }
}
