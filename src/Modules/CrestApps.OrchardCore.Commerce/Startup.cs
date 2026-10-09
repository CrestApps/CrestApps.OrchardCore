using CrestApps.OrchardCore.Commerce.Navigation;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Registers the shared Commerce admin menu that owns the top-level Commerce node and its icon, and the
/// shipped receipts-only financial-document policy, and the pieces shared by the commerce admin lists.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<CommerceAdminMenu>();
        services.AddScoped<IFinancialDocumentPolicy, ReceiptsOnlyFinancialDocumentPolicy>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, CommerceResourceManagementOptionsConfiguration>();
    }
}
