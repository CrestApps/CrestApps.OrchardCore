using Microsoft.Extensions.DependencyInjection.Extensions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Deployments;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Migrations;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Navigation;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Garnishments: orders withheld from pay within the law's limits and sent to their payees.
/// </summary>
[Feature(PayrollConstants.Features.Garnishments)]
public sealed class GarnishmentsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddGarnishmentsCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<GarnishmentOrder>, GarnishmentOrderHandler>());
        services.AddPayCodeKind(CrestApps.OrchardCore.Payroll.Models.PayCodeKinds.Garnishment, global::OrchardCore.Localization.LocalizationSource.Create<GarnishmentsStartup>("Garnishment"));
        services.AddDataMigration<GarnishmentsMigrations>()
            .AddIndexProvider<GarnishmentOrderIndexProvider>();
        services.AddDisplayDriver<GarnishmentOrder, GarnishmentOrderDisplayDriver>();
        services.AddNavigationProvider<GarnishmentsAdminMenu>();
    }
}

/// <summary>
/// Imports garnishment orders with a recipe step.
/// </summary>
[Feature(PayrollConstants.Features.Garnishments)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class GarnishmentsRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<GarnishmentsRecipeStep>();
}

/// <summary>
/// Exports garnishment orders with a deployment step.
/// </summary>
[Feature(PayrollConstants.Features.Garnishments)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class GarnishmentsDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<GarnishmentsDeploymentSource, GarnishmentsDeploymentStep, GarnishmentsDeploymentStepDisplayDriver>();
}

/// <summary>
/// Describes the garnishments steps to CrestApps Recipes.
/// </summary>
[Feature(PayrollConstants.Features.Garnishments)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class GarnishmentsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, GarnishmentsRecipeStepSchema>();
        services.AddDeploymentStepSchema<GarnishmentsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds the Garnishments data set to Payroll's data source.
/// </summary>
[Feature(PayrollConstants.Features.Garnishments)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class GarnishmentsDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IPayrollDataSet, GarnishmentsDataSet>();
}
