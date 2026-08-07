using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Rejects a shared health-check endpoint whose route claims liveness while reporting Contact Center
/// dependency checks.
/// </summary>
/// <remarks>
/// This runs only when the <c>OrchardCore.HealthChecks</c> feature is also enabled, because only then does the
/// unfiltered aggregate endpoint exist. Contact Center is what makes that endpoint dangerous, so it refuses to
/// introduce the hazard silently rather than relying on documentation an operator may never read.
/// </remarks>
[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class ContactCenterSharedHealthEndpointStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SharedHealthEndpointHazardState>();
        services.AddScoped<IModularTenantEvents, SharedHealthCheckEndpointValidator>();
        services.AddContactCenterSharedEndpointHealthCheck();
    }
}
