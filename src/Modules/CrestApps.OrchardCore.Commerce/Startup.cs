using CrestApps.OrchardCore.Commerce.Navigation;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Registers the shared Commerce admin menu that owns the top-level Commerce node and its icon, and the
/// shipped receipts-only financial-document policy, the pieces shared by the commerce admin lists, and the access
/// customers need to the admin pages of their own data.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<CommerceAdminMenu>();
        services.AddScoped<IFinancialDocumentPolicy, ReceiptsOnlyFinancialDocumentPolicy>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, CommerceResourceManagementOptionsConfiguration>();
        services.AddShapeTableProvider<PagerPageSizeSelectorShapeTableProvider>();
        services.AddScoped<IAuthorizationHandler, CustomerAdminPageAuthorizationHandler>();
    }
}
