using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Drivers;
using CrestApps.OrchardCore.Learning.Packages.Migrations;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.Recipes;
using CrestApps.OrchardCore.Learning.Packages.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Packages;

/// <summary>
/// Registers SCORM 1.2 and SCORM 2004 learning packages: the learning package course item with its part, the import page, the package
/// panel of the learner player, the sandboxed file endpoint and the runtime endpoint, the permission and personal data.
/// </summary>
[Feature(PackagesConstants.Features.Scorm)]
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(PackagesConstants.CollectionName));
        services.AddDataMigration<PackagesMigrations>()
            .AddIndexProvider<PackageRuntimeStateIndexProvider>();

        services.AddLearningPackagesCore();
        services.AddContentPart<LearningPackagePart>()
            .UseDisplayDriver<LearningPackagePartDisplayDriver>()
            .UseDisplayDriver<LearningPackagePartLearnerDisplayDriver>();

        services.AddPermissionProvider<PackagesPermissionProvider>();
        services.AddPersonalDataContributor<PackagesPersonalDataContributor>();
    }
}

/// <summary>
/// Imports learners' runtime states with a recipe step.
/// </summary>
[Feature(PackagesConstants.Features.Scorm)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<LearnerUserLookup>();
        services.AddRecipeExecutionStep<LearningPackageRuntimeStatesRecipeStep>();
    }
}

/// <summary>
/// Exports learners' runtime states with a deployment step.
/// </summary>
[Feature(PackagesConstants.Features.Scorm)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<LearnerUserLookup>();
        services.AddDeployment<LearningPackageRuntimeStatesDeploymentSource, LearningPackageRuntimeStatesDeploymentStep, LearningPackageRuntimeStatesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the runtime states recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(PackagesConstants.Features.Scorm)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningPackageRuntimeStatesRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningPackageRuntimeStatesDeploymentStepSchema>();
    }
}
