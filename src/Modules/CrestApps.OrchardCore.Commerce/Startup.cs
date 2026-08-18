using CrestApps.OrchardCore.Commerce.Navigation;
using CrestApps.OrchardCore.Commerce.FinancialDocuments;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Registers the shared Commerce admin menu that owns the top-level Commerce node and its icon, and the
/// shipped receipts-only financial-document policy.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<CommerceAdminMenu>();
        services.AddScoped<IFinancialDocumentPolicy, ReceiptsOnlyFinancialDocumentPolicy>();
    }
}
