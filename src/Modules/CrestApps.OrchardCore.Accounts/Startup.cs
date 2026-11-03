using CrestApps.OrchardCore.Accounts.Drivers;
using CrestApps.OrchardCore.Accounts.Endpoints;
using CrestApps.OrchardCore.Accounts.Handlers;
using CrestApps.OrchardCore.Accounts.Migrations;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Navigation;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.ContentTransfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes.Events;

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
        services.AddScoped<AccountListSynchronizer>();
        services.AddScoped<IContentDefinitionEventHandler, AccountsContentDefinitionEventHandler>();
        services.AddScoped<IContentDisplayDriver, AccountPickerDisplayDriver>();
        services.AddDataMigration<AccountMigrations>();
        services.AddNavigationProvider<AccountsAdminMenu>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddAccountSearchEndpoints();
    }
}

/// <summary>
/// Keeps the account lists up to date after a recipe defines content types.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeEventHandler, AccountsRecipeEventHandler>();
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
