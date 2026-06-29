using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.DialPad.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DialPad;

/// <summary>
/// Registers the DialPad implementation of the Contact Center dialer-agnostic provider.
/// </summary>
[Feature(DialPadConstants.Feature.Dialer)]
public sealed class DialerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDialerProvider, DialPadDialerProvider>();
    }
}
