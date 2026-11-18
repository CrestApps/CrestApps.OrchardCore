using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Core;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Drivers;
using CrestApps.OrchardCore.Receipts.Migrations;
using CrestApps.OrchardCore.Receipts.Navigation;
using CrestApps.OrchardCore.Receipts.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Receipts;

/// <summary>
/// Registers the reusable receipt builder, the receipt branding settings screen, and its permissions. Receipts are
/// printed as business documents of the <see cref="ReceiptsConstants.BusinessDocumentKind"/> kind.
/// </summary>
public sealed class Startup : StartupBase
{
    private static readonly LocalizationSource _receiptKindTitle = LocalizationSource.Create<Startup>("Receipts");

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReceiptService, DefaultReceiptService>();
        services.AddStoreLocations();
        services.AddScoped<IStoreSetupStep, StoreDetailsSetupStep>();
        services.AddMoneyFormatter();

        // The business document services a receipt is rendered with, registered here too so a site that had Receipts on
        // before it depended on Business Documents prints receipts before that feature is recorded as enabled.
        services.AddBusinessDocumentsCore();
        services.AddScoped<ReceiptBusinessDocumentMapper>();
        services.AddScoped<IReceiptHtmlRenderer, DefaultReceiptHtmlRenderer>();
        services.AddBusinessDocumentKind(ReceiptsConstants.BusinessDocumentKind, _receiptKindTitle);

        // A site that had Receipts on before it depended on Business Documents gets that feature recorded as enabled.
        services.AddDataMigration<ReceiptsBusinessDocumentsFeatureMigrations>();

        services
            .AddSiteDisplayDriver<ReceiptSettingsDisplayDriver>()
            .AddNavigationProvider<ReceiptsAdminMenu>()
            .AddPermissionProvider<ReceiptsPermissionProvider>();

        // Lets a role that holds only the receipt settings permission open Settings > Commerce > Receipts.
        services.AddSiteSettingsPermission(ReceiptsConstants.SettingsGroupId, ReceiptsPermissions.ManageReceiptSettings);
    }
}
