using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Drivers;
using CrestApps.OrchardCore.Learning.Packages.Migrations;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.Recipes;
using CrestApps.OrchardCore.Learning.Packages.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

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

        services.AddScoped<PackageContentHostProvider>();
        services.AddSiteDisplayDriver<LearningPackageContentHostSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(LearningPackageContentHostSettingsDisplayDriver.GroupId, LearningPackagesPermissions.ManageLearningPackageSettings);
        services.AddNavigationProvider<PackagesAdminMenu>();
    }
}

/// <summary>
/// Keeps the package content host and the site apart: it runs after the reverse proxy and security headers, and before
/// authentication, so a request on the content host never carries the learner's cookies into the site.
/// </summary>
[Feature(PackagesConstants.Features.Scorm)]
public sealed class ContentHostStartup : StartupBase
{
    /// <inheritdoc />
    public override int ConfigureOrder
        => OrchardCoreConstants.ConfigureOrder.Authentication - 10;

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => app.UseMiddleware<PackageContentHostMiddleware>();
}

/// <summary>
/// Registers cmi5 packages: the importer takes cmi5.xml, the launch and fetch endpoints, and the index of registrations.
/// </summary>
[Feature(PackagesConstants.Features.Cmi5)]
public sealed class Cmi5Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Cmi5Migrations>()
            .AddIndexProvider<Cmi5RegistrationIndexProvider>();

        services.AddLearningCmi5Core();
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
        services.AddScoped<ISiteSettingsSchemaDefinition, LearningPackageContentHostSettingsSchema>();
        services.AddDeploymentStepSchema<LearningPackageRuntimeStatesDeploymentStepSchema>();
    }
}
