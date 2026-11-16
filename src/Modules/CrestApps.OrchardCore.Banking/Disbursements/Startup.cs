using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Disbursements.AuditTrail;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Core.FileStorage;
using CrestApps.OrchardCore.Banking.Disbursements.Core;
using CrestApps.OrchardCore.Banking.Disbursements.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.DataPipelines;
using CrestApps.OrchardCore.Banking.Disbursements.Deployments;
using CrestApps.OrchardCore.Banking.Disbursements.Drivers;
using CrestApps.OrchardCore.Banking.Disbursements.Indexes;
using CrestApps.OrchardCore.Banking.Disbursements.Migrations;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Navigation;
using CrestApps.OrchardCore.Banking.Disbursements.Recipes;
using CrestApps.OrchardCore.Banking.Disbursements.Reports;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;
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

namespace CrestApps.OrchardCore.Banking.Disbursements;

/// <summary>
/// Registers disbursements, batches, their admin pages and the encrypted store of batch files.
/// </summary>
[Feature(DisbursementsConstants.Features.Disbursements)]
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
        services.AddScoped<DisbursementFileRecorder>();
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
[Feature(DisbursementsConstants.Features.DirectDeposit)]
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
[Feature(DisbursementsConstants.Features.DirectDeposit)]
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
[Feature(DisbursementsConstants.Features.Disbursements)]
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
[Feature(DisbursementsConstants.Features.Disbursements)]
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
[Feature(DisbursementsConstants.Features.Disbursements)]
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
[Feature(DisbursementsConstants.Features.Disbursements)]
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
[Feature(DisbursementsConstants.Features.Disbursements)]
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
[Feature(DisbursementsConstants.Features.Disbursements)]
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
[Feature(DisbursementsConstants.Features.Disbursements)]
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

/// <summary>
/// Writes each bank file that leaves the site to the Audit Trail.
/// </summary>
[Feature(DisbursementsConstants.Features.Disbursements)]
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AuditTrailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, DisbursementsAuditTrailEvents>();
    }
}
