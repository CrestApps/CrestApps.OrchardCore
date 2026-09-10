using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers only the channel-endpoint administration screen (the endpoint editor driver and its navigation).
/// The endpoint services and storage come from the Omnichannel Activities feature this depends on, so a feature
/// that needs to reuse channel endpoints can depend on this one feature instead of the full Omnichannel
/// management surface. Channel sources (SMS, Phone, ...) are registered by the features that own each channel, so
/// a channel only appears in the create picker when a feature that can actually use it is enabled.
/// </summary>
[Feature(OmnichannelConstants.Features.ChannelEndpoints)]
public sealed class ChannelEndpointsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<OmnichannelChannelEndpoint, OmnichannelChannelEndpointDisplayDriver>();
        services.AddNavigationProvider<ChannelEndpointsAdminMenu>();
    }
}
