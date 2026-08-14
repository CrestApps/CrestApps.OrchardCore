using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the distributed-dependency health checks that only apply once Redis backs the deployment.
/// </summary>
/// <remarks>
/// The distributed lock, Redis connectivity, and SignalR backplane probes depend on services that only the
/// <c>OrchardCore.Redis</c> feature registers, so they are gated here rather than in the base feature. This
/// mirrors how the Voice feature owns the provider-ingress check: a check must never be registered by a feature
/// whose dependency closure cannot construct it.
/// </remarks>
[RequireFeatures("OrchardCore.Redis", "OrchardCore.HealthChecks")]
public sealed class ContactCenterRedisHealthCheckStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterRedisHealthChecks();
    }
}
