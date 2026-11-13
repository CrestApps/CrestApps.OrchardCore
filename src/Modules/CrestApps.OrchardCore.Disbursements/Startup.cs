using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Checks;
using CrestApps.OrchardCore.Core.FileStorage;
using CrestApps.OrchardCore.Disbursements.Core;
using CrestApps.OrchardCore.Disbursements.Core.Services;
using CrestApps.OrchardCore.Disbursements.DataPipelines;
using CrestApps.OrchardCore.Disbursements.Deployments;
using CrestApps.OrchardCore.Disbursements.Drivers;
using CrestApps.OrchardCore.Disbursements.Indexes;
using CrestApps.OrchardCore.Disbursements.Migrations;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Navigation;
using CrestApps.OrchardCore.Disbursements.Recipes;
using CrestApps.OrchardCore.Disbursements.Reports;
using CrestApps.OrchardCore.Disbursements.Services;
using CrestApps.OrchardCore.Disbursements.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Disbursements;

/// <summary>
/// Registers disbursements, batches, their admin pages and the encrypted store of batch files.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(DisbursementsConstants.CollectionName));
        services.AddDataMigration<DisbursementsMigrations>()
            .AddIndexProvider<DisbursementIndexProvider>()
            .AddIndexProvider<DisbursementBatchIndexProvider>();

        services.AddDisbursementsCore();
        services.AddHttpContextAccessor();
        services.AddTenantFileStore<IDisbursementFileStore>(
            DisbursementsConstants.FileStoreFolder,
            configurationSection: null,
            (serviceProvider, fileStore) => new DisbursementFileStore(new EncryptedFileStore(fileStore, serviceProvider.GetRequiredService<IDataProtectionProvider>(), DisbursementsConstants.FileProtectionPurpose)));
        services.AddDisplayDriver<Disbursement, DisbursementDisplayDriver>();
        services.AddDisplayDriver<DisbursementBatch, DisbursementBatchDisplayDriver>();
        services.AddPermissionProvider<DisbursementsPermissionProvider>();
        services.AddNavigationProvider<DisbursementsAdminMenu>();
    }
}

/// <summary>
/// Sends batches as bank files (Nacha), imports return and change files and checks payee accounts with test entries.
/// </summary>
[Feature(DisbursementsConstants.Features.Nacha)]
public sealed class NachaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisbursementsNacha();
        services.AddDisplayDriver<BankAccount, NachaOriginSettingsDisplayDriver>();
    }
}

/// <summary>
/// Adds the Read a disbursement batch file step to Data Pipelines, so a pipeline delivers bank files.
/// </summary>
[Feature(DisbursementsConstants.Features.Nacha)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelines)]
public sealed class NachaDataPipelinesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ReadDisbursementBatchFileStep>();
        services.Configure<DataPipelineStepTypeOptions>(options => options.Add(DataPipelineStepTypeDescriptor.Create<ReadDisbursementBatchFileStep>()));
    }
}

/// <summary>
/// Pays batches by printed check when Checks is on.
/// </summary>
[RequireFeatures(ChecksConstants.Features.Checks)]
public sealed class ChecksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisbursementsChecks();
    }
}

/// <summary>
/// Starts workflows when disbursements are submitted, settle, are returned or fail, and settles batches from a task.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<DisbursementSubmittedEvent, DisbursementEventActivityDisplayDriver<DisbursementSubmittedEvent>>();
        services.AddActivity<DisbursementSettledEvent, DisbursementEventActivityDisplayDriver<DisbursementSettledEvent>>();
        services.AddActivity<DisbursementReturnedEvent, DisbursementEventActivityDisplayDriver<DisbursementReturnedEvent>>();
        services.AddActivity<DisbursementFailedEvent, DisbursementEventActivityDisplayDriver<DisbursementFailedEvent>>();
        services.AddActivity<SettleDisbursementBatchesTask, SettleDisbursementBatchesTaskDisplayDriver>();
        services.AddDisbursementObserver<DisbursementsWorkflowObserver>();
    }
}

/// <summary>
/// Describes the Disbursements workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<DisbursementSubmittedEventSchema>()
            .AddWorkflowActivitySchema<DisbursementSettledEventSchema>()
            .AddWorkflowActivitySchema<DisbursementReturnedEventSchema>()
            .AddWorkflowActivitySchema<DisbursementFailedEventSchema>()
            .AddWorkflowActivitySchema<SettleDisbursementBatchesTaskSchema>();
    }
}

/// <summary>
/// Imports disbursements paid elsewhere with a recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<DisbursementsRecipeStep>();
    }
}

/// <summary>
/// Exports disbursements with an opt-in deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<DisbursementsDeploymentSource, DisbursementsDeploymentStep, DisbursementsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Disbursements recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, DisbursementsRecipeStepSchema>();
        services.AddDeploymentStepSchema<DisbursementsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the Disbursements data source.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<DisbursementsReportDataSource>();
        services.AddScoped<IDisbursementsReportDataSet, DisbursementsReportDataSet>();
    }
}
