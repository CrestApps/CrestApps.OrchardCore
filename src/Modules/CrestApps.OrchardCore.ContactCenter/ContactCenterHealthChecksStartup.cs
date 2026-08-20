using CrestApps.OrchardCore.ContactCenter.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the health checks owned by the base Contact Center feature and maps the Contact Center
/// readiness and dependency probes, but only when the <c>OrchardCore.HealthChecks</c> feature is also
/// enabled so a deployment that does not use health checks never pays for them. The endpoints map here —
/// rather than in the base feature's <c>Configure</c> — because <c>MapHealthChecks</c> resolves the
/// <c>HealthCheckService</c> that only exists once <c>OrchardCore.HealthChecks</c> has registered it; mapping
/// them unconditionally threw at pipeline build time when health checks were not enabled.
/// </summary>
[RequireFeatures(ContactCenterConstants.Feature.Area, "OrchardCore.HealthChecks")]
public sealed class ContactCenterHealthChecksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterHealthChecks();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddContactCenterHealthEndpoints();
    }
}
