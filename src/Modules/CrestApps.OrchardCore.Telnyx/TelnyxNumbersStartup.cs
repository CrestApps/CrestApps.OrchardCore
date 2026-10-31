using CrestApps.OrchardCore.Telnyx.Drivers;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the Telnyx numbers page: the account's numbers, buying one, its caller name and releasing it.
/// </summary>
public sealed class TelnyxNumbersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDisplayDriver<TelnyxOwnedNumber, TelnyxOwnedNumberDisplayDriver>()
            .AddDisplayDriver<TelnyxAvailableNumber, TelnyxAvailableNumberDisplayDriver>()
            .AddNavigationProvider<TelnyxNumbersAdminMenu>();
    }
}
