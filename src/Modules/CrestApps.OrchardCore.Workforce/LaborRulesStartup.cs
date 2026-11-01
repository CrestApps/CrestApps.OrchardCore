using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Navigation;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers the labor rule catalog, rule sets, the checks on publishing and on taking shifts, overrides,
/// predictability pay, and their admin screens.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
public sealed class LaborRulesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkforceLaborRulesCore();
        services.AddDataMigration<LaborRulesMigrations>()
            .AddIndexProvider<LaborRuleSetIndexProvider>()
            .AddIndexProvider<LaborRuleOverrideIndexProvider>()
            .AddIndexProvider<PredictabilityPayEntryIndexProvider>();

        services.AddDisplayDriver<LaborRuleSet, LaborRuleSetRowDisplayDriver>();
        services.AddDisplayDriver<PredictabilityPayRowViewModel, PredictabilityPayRowDisplayDriver>();
        services.AddDisplayDriver<LaborRuleOverrideRowViewModel, LaborRuleOverrideRowDisplayDriver>();
        services.AddPermissionProvider<LaborRulesPermissionsProvider>();
        services.AddNavigationProvider<LaborRulesAdminMenu>();
    }
}

/// <summary>
/// Imports labor rule sets with the WorkforceLaborRules recipe step.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class LaborRulesRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<LaborRulesRecipeStep>();
    }
}

/// <summary>
/// Describes the WorkforceLaborRules recipe step to CrestApps Recipes.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class LaborRulesRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CrestApps.OrchardCore.Recipes.Core.IRecipeStep, LaborRulesRecipeStepSchema>();
    }
}
