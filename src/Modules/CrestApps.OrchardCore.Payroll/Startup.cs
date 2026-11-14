using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
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
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Registers Pay Setup: pay schedules and pay codes, their pages and permissions.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
public sealed class PaySetupStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(PayrollConstants.CollectionName));
        services.AddDataMigration<PaySetupMigrations>()
            .AddIndexProvider<PayScheduleIndexProvider>()
            .AddIndexProvider<PayCodeIndexProvider>();

        services.AddPaySetupCore();
        services.AddDisplayDriver<PaySchedule, PayScheduleDisplayDriver>();
        services.AddDisplayDriver<PayCode, PayCodeDisplayDriver>();
        services.AddPermissionProvider<PaySetupPermissionProvider>();
        services.AddNavigationProvider<PaySetupAdminMenu>();
    }
}

/// <summary>
/// Imports pay schedules and pay codes with recipe steps.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class PaySetupRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<PaySchedulesRecipeStep>();
        services.AddRecipeExecutionStep<PayCodesRecipeStep>();
    }
}

/// <summary>
/// Exports pay schedules and pay codes with deployment steps.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class PaySetupDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<PaySchedulesDeploymentSource, PaySchedulesDeploymentStep, SummaryDeploymentStepDisplayDriver<PaySchedulesDeploymentStep>>();
        services.AddDeployment<PayCodesDeploymentSource, PayCodesDeploymentStep, SummaryDeploymentStepDisplayDriver<PayCodesDeploymentStep>>();
    }
}

/// <summary>
/// Describes the Pay Setup recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class PaySetupRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, PaySchedulesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, PayCodesRecipeStepSchema>();
        services.AddDeploymentStepSchema<PaySchedulesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<PayCodesDeploymentStepSchema>();
    }
}
