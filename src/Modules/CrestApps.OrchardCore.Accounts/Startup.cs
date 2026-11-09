using CrestApps.OrchardCore.Accounts.Deployments;
using CrestApps.OrchardCore.Accounts.Drivers;
using CrestApps.OrchardCore.Accounts.Endpoints;
using CrestApps.OrchardCore.Accounts.Handlers;
using CrestApps.OrchardCore.Accounts.Indexes;
using CrestApps.OrchardCore.Accounts.Migrations;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Navigation;
using CrestApps.OrchardCore.Accounts.Recipes;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts;

/// <summary>
/// Registers the account part, the <c>Account</c> type, the account picker and search, and the account lists.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<AccountPart>()
            .AddHandler<AccountPartHandler>();

        services.AddScoped<IAccountContentTypeProvider, AccountContentTypeProvider>();

        // Accounts are parties: pickers and lists in any module (asset owners, work orders) find and name them.
        services.AddPartySource<AccountPartySource>();
        services.AddScoped<AccountListSynchronizer>();
        services.AddScoped<IContentDefinitionEventHandler, AccountsContentDefinitionEventHandler>();
        services.AddScoped<IContentDisplayDriver, AccountPickerDisplayDriver>();
        services.AddDataMigration<AccountMigrations>();
        services.AddNavigationProvider<AccountsAdminMenu>();
        services.AddShapeTableProvider<AccountPageShapeTableProvider>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddAccountSearchEndpoints();
    }
}

/// <summary>
/// Registers the members part, its editor and index, and the service that finds the account a user signs in for.
/// </summary>
[Feature(AccountsConstants.MembersFeature)]
public sealed class MembersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<AccountMembersPart>()
            .UseDisplayDriver<AccountMembersPartDisplayDriver>()
            .AddHandler<AccountMembersPartHandler>();

        services.AddScoped<IAccountMemberService, AccountMemberService>();
        services.AddIndexProvider<AccountMemberIndexProvider>();
        services.AddDataMigration<AccountMembersMigrations>();
        services.AddPermissionProvider<AccountMembersPermissionProvider>();
    }
}

/// <summary>
/// Registers the locations part and its editor.
/// </summary>
[Feature(AccountsConstants.LocationsFeature)]
public sealed class LocationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<AccountLocationsPart>()
            .UseDisplayDriver<AccountLocationsPartDisplayDriver>();

        services.AddDataMigration<AccountLocationsMigrations>();
    }
}

/// <summary>
/// Keeps the account lists up to date after a recipe defines content types, and registers the Accounts recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStepHandler, AccountsRecipeStepHandler>();
        services.AddRecipeExecutionStep<AccountsStep>();
    }
}

/// <summary>
/// Registers the Accounts deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AccountsDeploymentSource, AccountsDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, AccountsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Accounts recipe and deployment steps to CrestApps Recipes, so they can be checked and written with
/// help.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AccountsRecipeStepSchema>();
        services.AddDeploymentStepSchema<AccountsDeploymentStepSchema>();
        services.AddScoped<IContentSchemaDefinition, AccountPartSchemaDefinition>();
    }
}

/// <summary>
/// Describes the account members part to CrestApps Recipes.
/// </summary>
[Feature(AccountsConstants.MembersFeature)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class MembersRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, AccountMembersPartSchemaDefinition>();
    }
}

/// <summary>
/// Describes the account locations part to CrestApps Recipes.
/// </summary>
[Feature(AccountsConstants.LocationsFeature)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class LocationsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, AccountLocationsPartSchemaDefinition>();
    }
}

/// <summary>
/// Adds the <c>Account</c> column to the import and export of the records that can belong to an account.
/// </summary>
[RequireFeatures(ContentTransferConstants.Feature.ModuleId)]
public sealed class ContentTransferStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentImportHandler, AccountContentImportHandler>();
    }
}
