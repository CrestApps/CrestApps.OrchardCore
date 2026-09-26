using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the real-time supervisor dashboard, navigation, and monitoring endpoints.
/// </summary>
[Feature(ContactCenterConstants.Feature.Supervision)]
public sealed class SupervisionStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<ContactCenterSupervisionAdminMenu>();

        // Taking a call over, ending, transferring and recording it, and acting on an agent, from the live dashboard.
        services
            .AddScoped<IContactCenterSupervisorInterventionService, ContactCenterSupervisorInterventionService>()
            .AddScoped<SupervisorDashboardInterventionDescriber>()
            .AddScoped<SupervisorInterventionEndpoints.CallScope>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddSupervisorDashboardEndpoints()
            .AddSupervisorInterventionEndpoints();
    }
}

/// <summary>
/// Shows a supervisor's own engagement in their soft phone -- the call they are listening to, a mode switcher and Stop --
/// and makes the phone answer the leg it is rung on for it. This is integration glue that activates whenever the
/// supervisor dashboard and the Telephony soft phone are both enabled.
/// </summary>
[Feature(ContactCenterConstants.Feature.Supervision)]
[RequireFeatures(TelephonyConstants.Feature.SoftPhoneCore)]
public sealed class SupervisionSoftPhoneStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<SoftPhoneWidget, ContactCenterSupervisorSoftPhoneDisplayDriver>();
        services.AddResourceConfiguration<ContactCenterSupervisorSoftPhoneResourceConfiguration>();
    }
}
