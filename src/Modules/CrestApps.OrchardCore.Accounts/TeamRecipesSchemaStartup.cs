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
/// Describes the account team part to CrestApps Recipes.
/// </summary>
[Feature(AccountsConstants.TeamFeature)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TeamRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, AccountTeamPartSchemaDefinition>();
    }
}
