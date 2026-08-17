using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Drivers;
using CrestApps.OrchardCore.Receipts.Navigation;
using CrestApps.OrchardCore.Receipts.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Receipts;

/// <summary>
/// Registers the reusable receipt builder, the receipt branding settings screen, and its permissions.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReceiptService, DefaultReceiptService>();

        services
            .AddSiteDisplayDriver<ReceiptSettingsDisplayDriver>()
            .AddNavigationProvider<ReceiptsAdminMenu>()
            .AddPermissionProvider<ReceiptsPermissionProvider>();
    }
}
