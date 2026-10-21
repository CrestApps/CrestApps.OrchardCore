using CrestApps.OrchardCore.Commerce.Drivers;
using CrestApps.OrchardCore.Commerce.Navigation;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Commerce.Services;
using CrestApps.OrchardCore.Customers.PersonalData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Registers the shared Commerce admin menu that owns the top-level Commerce node and its icon, and the
/// shipped receipts-only financial-document policy, the pieces shared by the commerce admin lists, the access
/// customers need to the admin pages of their own data, and the export and anonymization of a person's commerce data.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<CommerceAdminMenu>();
        services.AddNavigationProvider<CommerceAccountMenu>();
        services.AddDisplayDriver<UserMenu, CommerceAccountUserMenuDisplayDriver>();
        services.AddScoped<IFinancialDocumentPolicy, ReceiptsOnlyFinancialDocumentPolicy>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, CommerceResourceManagementOptionsConfiguration>();
        services.AddScoped<IAuthorizationHandler, CustomerAdminPageAuthorizationHandler>();

        // Exports and anonymizes a person's commerce data. Each enabled commerce feature adds its own records through
        // an ICommercePersonalDataContributor, so the service sees only what is enabled.
        services.AddScoped<ICommercePersonalDataService, DefaultCommercePersonalDataService>();
    }
}
