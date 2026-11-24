using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Migrations;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
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
/// Tax Forms: forms made of fields worked out by formulas, the page that edits them, and the services a source (such as
/// Payroll) prepares them with.
/// </summary>
[Feature(TaxationConstants.Feature.TaxForms)]
public sealed class TaxFormsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTaxFormsCore();

        // The forms live in their own YesSql collection, created with the tenant's store.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TaxationConstants.TaxFormsCollectionName));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TaxForm>, TaxFormHandler>());
        services.AddDataMigration<TaxFormMigrations>()
            .AddIndexProvider<TaxFormIndexProvider>();
        services.AddDisplayDriver<TaxForm, TaxFormDisplayDriver>();
        services.AddNavigationProvider<TaxFormsAdminMenu>();
        services.AddPermissionProvider<TaxFormsPermissionsProvider>();
    }
}

/// <summary>
/// Imports tax forms with a recipe step.
/// </summary>
[Feature(TaxationConstants.Feature.TaxForms)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class TaxFormsRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<TaxFormsRecipeStep>();
}

/// <summary>
/// Exports tax forms with a deployment step.
/// </summary>
[Feature(TaxationConstants.Feature.TaxForms)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class TaxFormsDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<TaxFormsDeploymentSource, TaxFormsDeploymentStep, SummaryDeploymentStepDisplayDriver<TaxFormsDeploymentStep>>();
}

/// <summary>
/// Describes the tax forms steps to CrestApps Recipes.
/// </summary>
[Feature(TaxationConstants.Feature.TaxForms)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TaxFormsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TaxFormsRecipeStepSchema>();
        services.AddDeploymentStepSchema<TaxFormsDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds Taxation &gt; Tax Forms.
/// </summary>
internal sealed class TaxFormsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TaxFormsAdminMenu(IStringLocalizer<TaxFormsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.AddTaxation(S, taxation => taxation
            .Add(S["Tax Forms"], S["Tax Forms"].PrefixPosition(), forms => forms
                .Id("taxationTaxForms")
                .Action("Index", "TaxForms", "CrestApps.OrchardCore.Taxation")
                .Permission(TaxationPermissions.ManageTaxForms)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The permission to manage tax forms.
/// </summary>
internal sealed class TaxFormsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TaxationPermissions.ManageTaxForms,
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
