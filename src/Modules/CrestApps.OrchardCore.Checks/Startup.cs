using CrestApps.OrchardCore.Checks.Controllers;
using CrestApps.OrchardCore.Checks.Core;
using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Checks.Deployments;
using CrestApps.OrchardCore.Checks.Drivers;
using CrestApps.OrchardCore.Checks.Indexes;
using CrestApps.OrchardCore.Checks.Migrations;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Navigation;
using CrestApps.OrchardCore.Checks.Recipes;
using CrestApps.OrchardCore.Checks.Reports;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Checks.Workflows;
using CrestApps.OrchardCore.Core.FileStorage;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Checks;

/// <summary>
/// Registers the check register, the layouts, printing, their admin pages and the encrypted store of layout files.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ChecksConstants.CollectionName));
        services.AddDataMigration<ChecksMigrations>()
            .AddIndexProvider<CheckRecordIndexProvider>();

        services.AddChecksCore();
        services.AddHttpContextAccessor();
        services.AddTenantFileStore<ICheckFileStore>(
            ChecksConstants.FileStoreFolder,
            configurationSection: null,
            (serviceProvider, fileStore) => new CheckFileStore(new EncryptedFileStore(fileStore, serviceProvider.GetRequiredService<IDataProtectionProvider>(), ChecksConstants.FileProtectionPurpose)));
        services.AddDisplayDriver<CheckLayout, CheckLayoutDisplayDriver>();
        services.AddDisplayDriver<CheckRecord, CheckRecordDisplayDriver>();
        services.AddPermissionProvider<ChecksPermissionProvider>();
        services.AddNavigationProvider<ChecksAdminMenu>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ChecksResourceManagementOptionsConfiguration>();
    }
}

/// <summary>
/// Starts workflows when checks are issued, printed, voided and cleared.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<CheckIssuedEvent, CheckEventActivityDisplayDriver<CheckIssuedEvent>>();
        services.AddActivity<CheckPrintedEvent, CheckEventActivityDisplayDriver<CheckPrintedEvent>>();
        services.AddActivity<CheckVoidedEvent, CheckEventActivityDisplayDriver<CheckVoidedEvent>>();
        services.AddActivity<CheckClearedEvent, CheckEventActivityDisplayDriver<CheckClearedEvent>>();
        services.AddActivity<MarkStaleChecksTask, MarkStaleChecksTaskDisplayDriver>();
        services.AddCheckObserver<ChecksWorkflowObserver>();
    }
}

/// <summary>
/// Describes the Checks workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<CheckIssuedEventSchema>()
            .AddWorkflowActivitySchema<CheckPrintedEventSchema>()
            .AddWorkflowActivitySchema<CheckVoidedEventSchema>()
            .AddWorkflowActivitySchema<CheckClearedEventSchema>()
            .AddWorkflowActivitySchema<MarkStaleChecksTaskSchema>();
    }
}

/// <summary>
/// Imports check layouts and a check register with recipe steps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<CheckLayoutsRecipeStep>();
        services.AddRecipeExecutionStep<CheckRegisterRecipeStep>();
    }
}

/// <summary>
/// Exports check layouts and the check register with deployment steps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<CheckLayoutsDeploymentSource, CheckLayoutsDeploymentStep>();
        services.AddDeployment<CheckRegisterDeploymentSource, CheckRegisterDeploymentStep, CheckRegisterDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Checks recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, CheckLayoutsRecipeStepSchema>();
        services.AddScoped<IRecipeStep, CheckRegisterRecipeStepSchema>();
        services.AddDeploymentStepSchema<CheckLayoutsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<CheckRegisterDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the Checks data source: the check register.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<ChecksReportDataSource>();
        services.AddScoped<IChecksReportDataSet, CheckRegisterReportDataSet>();
    }
}
