using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers only the channel-endpoint administration screen (the endpoint editor driver and its navigation).
/// The endpoint services and storage come from the Omnichannel Activities feature this depends on, so a feature
/// that needs to reuse channel endpoints can depend on this one feature instead of the full Omnichannel
/// management surface.
/// </summary>
[Feature(OmnichannelConstants.Features.ChannelEndpoints)]
public sealed class ChannelEndpointsStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public ChannelEndpointsStartup(IStringLocalizer<ChannelEndpointsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<OmnichannelChannelEndpoint, OmnichannelChannelEndpointDisplayDriver>();
        services.AddNavigationProvider<ChannelEndpointsAdminMenu>();

        // The Phone channel is the baseline source the channel-endpoint administration ships with (inbound voice
        // maps a dialed number to a subject flow). Other channels register their own source from their feature.
        services.AddChannelEndpointSource(OmnichannelConstants.Channels.Phone, source =>
        {
            source.DisplayName = S["Phone"];
            source.Description = S["A phone number for inbound voice. Routes a dialed number to a subject flow."];
        });
    }
}
