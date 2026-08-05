using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center projection that synchronizes server-side voice state with the Telephony soft phone.
/// This projection is integration glue that activates whenever Contact Center Voice, Contact Center Real-Time, and the
/// Telephony soft phone are all enabled, rather than a separately selectable feature.
/// </summary>
[RequireFeatures(
    ContactCenterConstants.Feature.Voice,
    ContactCenterConstants.Feature.RealTime,
    TelephonyConstants.Feature.SoftPhone)]
public sealed class VoiceSoftPhoneStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterEventHandler, ContactCenterSoftPhoneEventHandler>()
            .AddDisplayDriver<SoftPhoneWidget, ContactCenterSoftPhoneWidgetDisplayDriver>();

        services.AddResourceConfiguration<ContactCenterSoftPhoneResourceConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        routes.AddAgentSoftPhoneEndpoints(adminOptions.AdminUrlPrefix);
    }
}
