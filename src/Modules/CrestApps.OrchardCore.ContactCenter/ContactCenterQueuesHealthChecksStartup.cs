using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the health checks owned by the Contact Center Queues feature, but only when the
/// <c>OrchardCore.HealthChecks</c> feature is also enabled so a deployment that does not use health checks never
/// pays for them.
/// </summary>
[RequireFeatures(ContactCenterConstants.Feature.Queues, "OrchardCore.HealthChecks")]
public sealed class ContactCenterQueuesHealthChecksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterQueuesHealthChecks();
    }
}
