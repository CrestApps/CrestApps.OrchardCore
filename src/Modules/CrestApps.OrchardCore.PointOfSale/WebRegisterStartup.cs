using CrestApps.OrchardCore.PointOfSale.Navigation;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.PointOfSale;

/// <summary>
/// The web register: its page is served by <see cref="Controllers.WebRegisterController"/>, which only routes while
/// this feature is on.
/// </summary>
[Feature(PosConstants.WebRegisterFeature)]
public sealed class WebRegisterStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddNavigationProvider<PosWebRegisterMenu>();
}
