using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Drivers;
using CrestApps.OrchardCore.Learning.Xapi.Endpoints;
using CrestApps.OrchardCore.Learning.Xapi.Migrations;
using CrestApps.OrchardCore.Learning.Xapi.Navigation;
using CrestApps.OrchardCore.Learning.Xapi.Recipes;
using CrestApps.OrchardCore.Learning.Xapi.Reports;
using CrestApps.OrchardCore.Learning.Xapi.Services;
using CrestApps.OrchardCore.Learning.Xapi.ViewModels;
using CrestApps.OrchardCore.Learning.Xapi.Workflows;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.RateLimits;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning.Xapi;

/// <summary>
/// Registers the Learning Record Store: the store and its indexes, the LRS endpoint, the permissions and the admin list
/// of statements.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(XapiConstants.CollectionName));
        services.AddDataMigration<XapiMigrations>()
            .AddIndexProvider<XapiStatementIndexProvider>()
            .AddIndexProvider<XapiDocumentIndexProvider>()
            .AddIndexProvider<XapiActivityIndexProvider>();

        services.AddLearningXapiCore();
        services.AddPermissionProvider<XapiPermissionProvider>();
        services.AddNavigationProvider<XapiAdminMenu>();
        services.TryAddScoped<LearnerUserLookup>();
        services.AddDisplayDriver<XapiStatementRow, XapiStatementRowDisplayDriver>();
        services.AddPersonalDataContributor<XapiPersonalDataContributor>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapLearningXapi();
}

/// <summary>
/// Limits how many requests one address may send to the LRS, when Orchard Core's Rate Limits feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    /// <summary>
    /// How many LRS requests an address may send a minute: enough for a classroom behind one address.
    /// </summary>
    public const int RequestsPerMinute = 1200;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            XapiConstants.RateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(
                policyName: XapiConstants.RateLimitGroup,
                permitLimit: RequestsPerMinute,
                window: TimeSpan.FromMinutes(1))));
    }
}

/// <summary>
/// Adds the Learning statements data source of Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<LearningStatementsReportDataSet>();
        services.AddDataSource<LearningXapiDataSource>();
    }
}

/// <summary>
/// Imports statements with a recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<LearningStatementsRecipeStep>();
}

/// <summary>
/// Exports statements with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<LearningStatementsDeploymentSource, LearningStatementsDeploymentStep, LearningStatementsDeploymentStepDisplayDriver>();
}

/// <summary>
/// Describes the statements recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningStatementsRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningStatementsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the Learning Statement Stored workflow event.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<LearningStatementStoredEvent, LearningStatementStoredEventDisplayDriver>();
        services.AddXapiStatementObserver<LearningStatementWorkflowObserver>();
    }
}

/// <summary>
/// Describes the Learning Statement Stored event to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddWorkflowActivitySchema<LearningStatementStoredEventSchema>();
}
