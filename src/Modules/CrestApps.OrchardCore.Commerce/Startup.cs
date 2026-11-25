using CrestApps.OrchardCore.Commerce.Navigation;
using CrestApps.OrchardCore.Commerce.Services;
using CrestApps.OrchardCore.Core.ModelBinding;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.PersonalData.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// The Commerce Foundation feature: the shared Commerce admin menu that owns the top-level Commerce node and its icon,
/// the pieces shared by the commerce admin lists (the customer's My Account area is the Users module's), the access customers need to the admin pages of
/// their own data, and the export and anonymization of a person's commerce data. Billing turns it on without the store.
/// </summary>
[Feature(CommerceConstants.Features.Foundation)]
public sealed class FoundationStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<CommerceAdminMenu>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, CommerceResourceManagementOptionsConfiguration>();
        services.AddScoped<IAuthorizationHandler, CustomerAdminPageAuthorizationHandler>();
        services.AddShapeTableProvider<AdminListFiltersShapeTableProvider>();

        // Exports and anonymizes a person's commerce data. Each enabled commerce feature adds its own records through
        // an ICommercePersonalDataContributor, so the service sees only what is enabled.
        services.AddScoped<ICommercePersonalDataService, DefaultCommercePersonalDataService>();

        // The commerce records reach every privacy request through the one personal-data service.
        services.AddPersonalDataContributor<CommercePersonalDataContributor>();
    }
}

/// <summary>
/// The Commerce feature, the store layer: the store's setup guide under Commerce › Set Up Your Store, whose steps come
/// from the modules they're about, and its permission and the staff roles it adds.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInvariantNumberInputs();

        services.AddNavigationProvider<StoreSetupAdminMenu>();
        services.AddPermissionProvider<CommercePermissionProvider>();

        // A tenant that ran Commerce before My Account moved to Users gets that feature turned on when it starts.
        services.AddScoped<IModularTenantEvents, CommerceAccountFeatureUpgrade>();
    }
}

/// <summary>
/// Adds roles for a store's staff from the setup guide.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Roles)]
public sealed class StaffRolesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IStoreSetupStep, StaffRolesSetupStep>();
}
