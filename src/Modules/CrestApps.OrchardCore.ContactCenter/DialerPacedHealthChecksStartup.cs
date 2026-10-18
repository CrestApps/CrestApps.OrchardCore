using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the dialer pacing heartbeat check owned by the Contact Center Paced Dialing feature, but only when the
/// <c>OrchardCore.HealthChecks</c> feature is also enabled so a deployment that does not use health checks never pays for
/// it.
/// </summary>
[Feature(ContactCenterConstants.Feature.DialerPaced)]
[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class DialerPacedHealthChecksStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterDialerPacingHealthChecks();
    }
}
