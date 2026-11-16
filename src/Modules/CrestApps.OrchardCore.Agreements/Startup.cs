using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Deployments;
using CrestApps.OrchardCore.Agreements.Drivers;
using CrestApps.OrchardCore.Agreements.Migrations;
using CrestApps.OrchardCore.Agreements.Navigation;
using CrestApps.OrchardCore.Agreements.Recipes;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Agreements;

/// <summary>
/// Agreements with accounts: their store, pages, life cycle, recipe and deployment steps, and the Agreements tab of an
/// account.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(AgreementsConstants.CollectionName));
        services.AddAgreementsCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Agreement>, AgreementHandler>());
        services.AddPermissionProvider<AgreementsPermissionsProvider>();
        services.AddIndexProvider<AgreementIndexProvider>();
        services.AddDataMigration<AgreementsMigrations>();
        services.AddDisplayDriver<Agreement, AgreementDisplayDriver>();
        services.AddDisplayDriver<Agreement, AgreementHoursDisplayDriver>();
        services.AddNavigationProvider<AgreementsAdminMenu>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AgreementsBackgroundTask>());
        services.AddScoped<IContentDisplayDriver, AccountAgreementsDisplayDriver>();

        services.AddRecipeExecutionStep<AgreementsRecipeStep>();
        services.AddScoped<IRecipeStep, AgreementsRecipeStepSchema>();
        services.AddDeployment<AgreementsDeploymentSource, AgreementsDeploymentStep, AgreementsDeploymentStepDisplayDriver>();
        services.AddDeploymentStepSchema<AgreementsDeploymentStepSchema>();
    }
}

/// <summary>
/// With Notifications on, the account's manager hears when an agreement passes 80% and 100% of its hours or is about to
/// end.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class AgreementNotificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IAgreementObserver, AgreementAccountManagerNotifier>();
}

/// <summary>
/// Provides the agreement permissions.
/// </summary>
internal sealed class AgreementsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AgreementsPermissions.ManageAgreements,
        AgreementsPermissions.ViewAgreements,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = global::OrchardCore.OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}

/// <summary>
/// Keeps agreements current: expires the ones past their end, and tells observers when one is about to end or passes
/// 80% and 100% of its hours.
/// </summary>
[BackgroundTask(
    Title = "Agreements",
    Schedule = "10 * * * *",
    Description = "Expires agreements past their end, and tells when one is about to end or passes 80% and 100% of its hours.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class AgreementsBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<CrestApps.OrchardCore.Agreements.Core.Services.AgreementLifecycle>().RunAsync(cancellationToken);
}
