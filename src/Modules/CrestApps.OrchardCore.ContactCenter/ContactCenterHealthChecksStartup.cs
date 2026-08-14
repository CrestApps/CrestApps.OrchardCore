using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the health checks owned by the base Contact Center feature, but only when the
/// <c>OrchardCore.HealthChecks</c> feature is also enabled so a deployment that does not use health checks never
/// pays for them.
/// </summary>
[RequireFeatures(ContactCenterConstants.Feature.Area, "OrchardCore.HealthChecks")]
public sealed class ContactCenterHealthChecksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterHealthChecks();
    }
}
