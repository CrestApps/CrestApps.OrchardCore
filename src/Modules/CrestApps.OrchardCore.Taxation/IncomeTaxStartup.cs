using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Migrations;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Income Tax: the income tax rules, their pages and the built-in income tax provider.
/// </summary>
[Feature(TaxationConstants.Feature.IncomeTax)]
public sealed class IncomeTaxStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIncomeTaxCore();

        // The rules live in their own YesSql collection, created with the tenant's store.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TaxationConstants.IncomeTaxCollectionName));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<IncomeTaxRule>, IncomeTaxRuleHandler>());
        services.AddDataMigration<IncomeTaxMigrations>()
            .AddIndexProvider<IncomeTaxRuleIndexProvider>();
        services.AddDisplayDriver<IncomeTaxRule, IncomeTaxRuleDisplayDriver>();
        services.AddNavigationProvider<IncomeTaxAdminMenu>();
        services.AddPermissionProvider<IncomeTaxPermissionsProvider>();
    }
}

/// <summary>
/// Imports income tax rules with a recipe step.
/// </summary>
[Feature(TaxationConstants.Feature.IncomeTax)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class IncomeTaxRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<IncomeTaxRulesRecipeStep>();
}

/// <summary>
/// Exports income tax rules with a deployment step.
/// </summary>
[Feature(TaxationConstants.Feature.IncomeTax)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class IncomeTaxDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<IncomeTaxRulesDeploymentSource, IncomeTaxRulesDeploymentStep, SummaryDeploymentStepDisplayDriver<IncomeTaxRulesDeploymentStep>>();
}

/// <summary>
/// Describes the income tax rules steps to CrestApps Recipes.
/// </summary>
[Feature(TaxationConstants.Feature.IncomeTax)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class IncomeTaxRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, IncomeTaxRulesRecipeStepSchema>();
        services.AddDeploymentStepSchema<IncomeTaxRulesDeploymentStepSchema>();
    }
}

/// <summary>
/// United States Income Tax: Form W-4 as the federal election and its withholding method, and backup withholding on
/// contractors. The federal rules come as dated recipes.
/// </summary>
[Feature(TaxationConstants.Feature.IncomeTaxUnitedStates)]
public sealed class UnitedStatesIncomeTaxStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddElectionFieldSet(UnitedStatesElectionFields.Create());
        services.AddIncomeTaxWithholdingMethod<UnitedStatesFormW4WithholdingMethod>();
        services.AddIncomeTaxCategory(IncomeTaxCategories.ContractorWithholding, global::OrchardCore.Localization.LocalizationSource.Create<UnitedStatesIncomeTaxStartup>("Backup withholding (contractors)"));
    }
}

/// <summary>
/// Canada Income Tax: the TD1 and TP-1015.3 claims, the CRA's federal, provincial and territorial formulas, Revenu
/// Québec's formulas, and the contributions (CPP, QPP, EI, QPIP) as rules. The yearly figures come as dated recipes.
/// </summary>
[Feature(TaxationConstants.Feature.IncomeTaxCanada)]
public sealed class CanadaIncomeTaxStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddCanadaIncomeTax();
}

/// <summary>
/// Adds Taxes &gt; Income tax rules.
/// </summary>
internal sealed class IncomeTaxAdminMenu : AdminNavigationProvider
{
    internal readonly Microsoft.Extensions.Localization.IStringLocalizer S;

    public IncomeTaxAdminMenu(Microsoft.Extensions.Localization.IStringLocalizer<IncomeTaxAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Taxes"], S["Taxes"].PrefixPosition(), taxes => taxes
                .Id("taxes")
                .AddClass("taxes")
                .Add(S["Income tax rules"], S["Income tax rules"].PrefixPosition(), rules => rules
                    .Id("taxationIncomeTaxRules")
                    .Action("Index", "IncomeTaxRules", "CrestApps.OrchardCore.Taxation")
                    .Permission(global::CrestApps.OrchardCore.Taxation.Core.TaxationPermissions.ManageIncomeTaxRules)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The permission to manage income tax rules.
/// </summary>
internal sealed class IncomeTaxPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        global::CrestApps.OrchardCore.Taxation.Core.TaxationPermissions.ManageIncomeTaxRules,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = global::OrchardCore.OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
