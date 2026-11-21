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
using CrestApps.OrchardCore.PageSections;
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
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts;

/// <summary>
/// Registers the account team part and its editor.
/// </summary>
[Feature(AccountsConstants.TeamFeature)]
public sealed class TeamStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<AccountTeamPart>()
            .UseDisplayDriver<AccountTeamPartDisplayDriver>();

        services.AddDataMigration<AccountTeamMigrations>();
    }
}
