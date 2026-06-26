using CrestApps.OrchardCore.DialPad.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DialPad;

/// <summary>
/// Registers the DialPad telephony provider and its settings driver.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDialPadTelephonyProvider()
            .AddSiteDisplayDriver<DialPadSettingsDisplayDriver>();
    }
}
