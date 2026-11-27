using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Core.Cohorts;
using CrestApps.OrchardCore.Users.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.Cohorts;

/// <summary>
/// Registers cohorts, their admin pages and the "Cohort member" audience rule.
/// </summary>
[Feature(UsersConstants.Feature.Cohorts)]
public sealed class CohortsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(UsersConstants.CohortsCollectionName));
        services.AddDataMigration<CohortsMigrations>()
            .AddIndexProvider<CohortIndexProvider>()
            .AddIndexProvider<CohortMemberIndexProvider>();
        services.AddCohorts();
        services.AddUserSearchService();
        services.AddPermissionProvider<CohortsPermissionProvider>();
        services.AddNavigationProvider<CohortsAdminMenu>();
        services.AddDisplayDriver<CohortRow, CohortRowDisplayDriver>();
        services.AddDisplayDriver<CohortEditViewModel, CohortEditDisplayDriver>();
        services.AddDisplayDriver<CohortMemberRow, CohortMemberRowDisplayDriver>();
    }
}

/// <summary>
/// Registers the cohorts recipe step.
/// </summary>
[Feature(UsersConstants.Feature.Cohorts)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class CohortsRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<UserCohortsRecipeStep>();
    }
}

/// <summary>
/// Registers the cohorts deployment step.
/// </summary>
[Feature(UsersConstants.Feature.Cohorts)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class CohortsDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<UserCohortsDeploymentSource, UserCohortsDeploymentStep, UserCohortsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the cohorts recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(UsersConstants.Feature.Cohorts)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class CohortsRecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, UserCohortsRecipeStepSchema>();
        services.AddDeploymentStepSchema<UserCohortsDeploymentStepSchema>();
    }
}

/// <summary>
/// Declares the permission that guards cohorts.
/// </summary>
internal sealed class CohortsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        UserPermissions.ManageCohorts,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
