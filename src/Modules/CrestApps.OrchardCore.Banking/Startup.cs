using CrestApps.OrchardCore.Banking.AuditTrail;
using CrestApps.OrchardCore.Banking.Core;
using CrestApps.OrchardCore.Banking.Deployments;
using CrestApps.OrchardCore.Banking.Drivers;
using CrestApps.OrchardCore.Banking.Endpoints;
using CrestApps.OrchardCore.Banking.Indexes;
using CrestApps.OrchardCore.Banking.Migrations;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Navigation;
using CrestApps.OrchardCore.Banking.Recipes;
using CrestApps.OrchardCore.Banking.Reports;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.Workflows;
using CrestApps.OrchardCore.Core.ModelBinding;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Banking;

/// <summary>
/// Registers bank accounts, their admin pages, their editor and the owner search.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(BankingConstants.CollectionName));
        services.AddDataMigration<BankingMigrations>()
            .AddIndexProvider<BankAccountIndexProvider>();

        services.AddBankingCore();
        // The disbursement, check and check layout editors post amounts from number inputs.
        services.AddInvariantNumberInputs();
        services.AddPartyDirectory();
        services.AddHttpContextAccessor();
        services.AddDisplayDriver<BankAccount, BankAccountDisplayDriver>();
        services.AddScoped<BankAccountLinkFlow>();
        services.AddPermissionProvider<BankingPermissionProvider>();
        services.AddNavigationProvider<BankingAdminMenu>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddBankAccountOwnerSearchEndpoint(serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix);
    }
}

/// <summary>
/// Writes bank account changes and reveals to the Audit Trail.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AuditTrailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, BankingAuditTrailEvents>();
        services.AddBankAccountObserver<AuditTrailBankAccountObserver>();
    }
}

/// <summary>
/// Starts workflows when bank accounts change.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<BankAccountChangedEvent, BankAccountChangedEventDisplayDriver>();
        services.AddActivity<BankAccountStatusChangedEvent, BankAccountStatusChangedEventDisplayDriver>();
        services.AddBankAccountObserver<BankingWorkflowObserver>();
    }
}

/// <summary>
/// Describes the Banking workflow event to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<BankAccountChangedEventSchema>();
        services.AddWorkflowActivitySchema<BankAccountStatusChangedEventSchema>();
    }
}

/// <summary>
/// Imports bank accounts with a recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<BankAccountsRecipeStep>();
    }
}

/// <summary>
/// Exports bank accounts with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<BankAccountsDeploymentSource, BankAccountsDeploymentStep, BankAccountsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Banking recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, BankAccountsRecipeStepSchema>();
        services.AddDeploymentStepSchema<BankAccountsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the Banking data source: the bank accounts, without their numbers.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<BankingReportDataSource>();
        services.AddScoped<IBankingReportDataSet, BankAccountsReportDataSet>();
    }
}
