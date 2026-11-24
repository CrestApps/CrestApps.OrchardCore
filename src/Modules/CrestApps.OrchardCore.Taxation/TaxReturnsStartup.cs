using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Migrations;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.BackgroundTasks;
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
/// Tax Returns: the periods each registration files for, the returns, their page and the filing schedule of a
/// registration.
/// </summary>
[Feature(TaxationConstants.Feature.Returns)]
public sealed class TaxReturnsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTaxReturnsCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TaxReturn>, TaxReturnHandler>());
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TaxationConstants.TaxReturnsCollectionName));
        services.AddDataMigration<TaxReturnsMigrations>()
            .AddIndexProvider<TaxReturnIndexProvider>();
        services.AddDisplayDriver<MerchantTaxRegistration, MerchantTaxRegistrationFilingDisplayDriver>();
        services.AddDisplayDriver<TaxReturnPeriodSummary, TaxReturnPeriodSummaryDisplayDriver>();
        services.AddDisplayDriver<TaxReturn, TaxReturnDisplayDriver>();
        services.AddDisplayDriver<TaxReturnPaymentEntry, TaxReturnPaymentEntryDisplayDriver>();
        services.AddNavigationProvider<TaxReturnsAdminMenu>();
        services.AddPermissionProvider<TaxReturnsPermissionsProvider>();
    }
}

/// <summary>
/// Imports tax returns with a recipe step.
/// </summary>
[Feature(TaxationConstants.Feature.Returns)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class TaxReturnsRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<TaxReturnsRecipeStep>();
}

/// <summary>
/// Exports tax returns with a deployment step.
/// </summary>
[Feature(TaxationConstants.Feature.Returns)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class TaxReturnsDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<TaxReturnsDeploymentSource, TaxReturnsDeploymentStep, SummaryDeploymentStepDisplayDriver<TaxReturnsDeploymentStep>>();
}

/// <summary>
/// Describes the tax returns steps to CrestApps Recipes.
/// </summary>
[Feature(TaxationConstants.Feature.Returns)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TaxReturnsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TaxReturnsRecipeStepSchema>();
        services.AddDeploymentStepSchema<TaxReturnsDeploymentStepSchema>();
    }
}

/// <summary>
/// Tax E-Filing: submissions, the manual channel, tax returns as targets with their JSON format, the e-filing card of a
/// return, and the task that asks channels for the agency's answers.
/// </summary>
[Feature(TaxationConstants.Feature.EFiling)]
public sealed class EFilingStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddEFilingCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<EFileSubmission>, EFileSubmissionHandler>());
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TaxationConstants.TaxReturnsCollectionName));
        services.AddDataMigration<EFilingMigrations>()
            .AddIndexProvider<EFileSubmissionIndexProvider>();
        services.AddDisplayDriver<TaxReturn, TaxReturnEFilingDisplayDriver>();
        services.AddDisplayDriver<EFileSubmissionRequest, EFileSubmissionRequestDisplayDriver>();
        services.AddSingleton<IBackgroundTask, EFileAcknowledgementBackgroundTask>();
    }
}

/// <summary>
/// Imports e-file submissions with a recipe step.
/// </summary>
[Feature(TaxationConstants.Feature.EFiling)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class EFilingRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<EFileSubmissionsRecipeStep>();
}

/// <summary>
/// Exports e-file submissions with a deployment step.
/// </summary>
[Feature(TaxationConstants.Feature.EFiling)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class EFilingDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<EFileSubmissionsDeploymentSource, EFileSubmissionsDeploymentStep, SummaryDeploymentStepDisplayDriver<EFileSubmissionsDeploymentStep>>();
}

/// <summary>
/// Describes the e-file submissions steps to CrestApps Recipes.
/// </summary>
[Feature(TaxationConstants.Feature.EFiling)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class EFilingRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, EFileSubmissionsRecipeStepSchema>();
        services.AddDeploymentStepSchema<EFileSubmissionsDeploymentStepSchema>();
    }
}

/// <summary>
/// Every 15 minutes: asks the filing channels that are not manual for the agency's answers to the submissions waiting
/// for one, and marks the accepted documents as filed.
/// </summary>
[BackgroundTask(
    Title = "Tax e-filing acknowledgements",
    Schedule = "*/15 * * * *",
    Description = "Asks the filing channels for the agency's answers to the tax returns filed electronically.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
internal sealed class EFileAcknowledgementBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 50;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var submissions = serviceProvider.GetRequiredService<IEFileSubmissionManager>();
        var eFiling = serviceProvider.GetRequiredService<IEFilingService>();

        foreach (var submission in await submissions.ListPendingAsync(BatchSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await eFiling.CheckAsync(submission, cancellationToken);
        }
    }
}

/// <summary>
/// Adds Commerce &gt; Taxation &gt; Tax Returns.
/// </summary>
internal sealed class TaxReturnsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TaxReturnsAdminMenu(IStringLocalizer<TaxReturnsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.AddTaxation(S, taxation => taxation
            .Add(S["Tax Returns"], S["Tax Returns"].PrefixPosition(), returns => returns
                .Id("taxationReturns")
                .Action("Index", "TaxReturns", "CrestApps.OrchardCore.Taxation")
                .Permission(TaxationPermissions.ManageTaxReturns)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The permissions to prepare and to file tax returns.
/// </summary>
internal sealed class TaxReturnsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TaxationPermissions.ManageTaxReturns,
        TaxationPermissions.FileTaxReturns,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
