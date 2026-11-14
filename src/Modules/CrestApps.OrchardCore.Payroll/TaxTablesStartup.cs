using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Payroll.Core.Services;
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
/// Payroll Tax Tables: the payroll tax rules, their pages and the built-in tax provider.
/// </summary>
[Feature(PayrollConstants.Features.TaxTables)]
public sealed class TaxTablesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPayrollTaxTablesCore();
        services.AddDataMigration<PayrollTaxTablesMigrations>()
            .AddIndexProvider<PayrollTaxRuleIndexProvider>();
        services.AddDisplayDriver<PayrollTaxRule, PayrollTaxRuleDisplayDriver>();
        services.AddNavigationProvider<PayrollTaxTablesAdminMenu>();
    }
}

/// <summary>
/// Imports payroll tax rules with a recipe step.
/// </summary>
[Feature(PayrollConstants.Features.TaxTables)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class TaxTablesRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<PayrollTaxRulesRecipeStep>();
}

/// <summary>
/// Exports payroll tax rules with a deployment step.
/// </summary>
[Feature(PayrollConstants.Features.TaxTables)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class TaxTablesDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<PayrollTaxRulesDeploymentSource, PayrollTaxRulesDeploymentStep, SummaryDeploymentStepDisplayDriver<PayrollTaxRulesDeploymentStep>>();
}

/// <summary>
/// Describes the payroll tax rules steps to CrestApps Recipes.
/// </summary>
[Feature(PayrollConstants.Features.TaxTables)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TaxTablesRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, PayrollTaxRulesRecipeStepSchema>();
        services.AddDeploymentStepSchema<PayrollTaxRulesDeploymentStepSchema>();
    }
}
