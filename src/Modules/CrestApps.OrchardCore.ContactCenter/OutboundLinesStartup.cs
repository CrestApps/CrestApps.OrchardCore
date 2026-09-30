using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Outbound Lines feature: each of the tenant's phone numbers can carry a line of agents who dial out
/// from it, and the soft phone and the dialer present that number as the caller ID of the calls those agents place.
/// </summary>
[Feature(ContactCenterConstants.Feature.OutboundLines)]
public sealed class OutboundLinesStartup : StartupBase
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundLinesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OutboundLinesStartup(IStringLocalizer<OutboundLinesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // A line is a phone number, so the Phone channel is offered in the channel-endpoint create picker even
        // without inbound voice. Inbound Voice registers the same source when it is enabled.
        services.AddChannelEndpointSource(OmnichannelConstants.Channels.Phone, source =>
        {
            source.DisplayName = S["Phone"];
            source.Description = S["A phone number the contact center owns. Agents can dial out from it, and with Inbound Voice it routes the calls it receives."];
        });

        // Replaces the Telephony default that gives nobody a line.
        services.Replace(ServiceDescriptor.Scoped<IOutboundLineResolver, ChannelEndpointOutboundLineResolver>());

        // The numbers agents dial out from belong to the tenant, so no transfer is ever sent back to one.
        services.AddScoped<IContactCenterOwnNumberSource, OutboundLineOwnNumberSource>();

        services.AddDisplayDriver<OmnichannelChannelEndpoint, OutboundLineEndpointDisplayDriver>();
        services.AddScoped<IChannelEndpointRule, OutboundLineEndpointRule>();
    }
}
