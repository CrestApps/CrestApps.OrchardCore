using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Tickets.Core;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Deployments;
using CrestApps.OrchardCore.Tickets.Drivers;
using CrestApps.OrchardCore.Tickets.Migrations;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Navigation;
using CrestApps.OrchardCore.Tickets.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// Registers the Tickets feature: the ticket part and index, the ticket service, the settings records and their pages,
/// the permissions and the menu.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCatalogs()
            .AddCatalogManagers();

        services.AddContentPart<TicketPart>();
        services.AddDataMigration<TicketsMigrations>()
            .AddIndexProvider<TicketIndexProvider>();

        services.AddTicketsCore();

        // The settings records' rules come with the feature that imports them.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketStatus>, TicketSettingsRecordHandler<TicketStatus>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketPriority>, TicketSettingsRecordHandler<TicketPriority>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketDesk>, TicketSettingsRecordHandler<TicketDesk>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketView>, TicketSettingsRecordHandler<TicketView>>());

        services.AddDisplayDriver<TicketStatus, TicketStatusDisplayDriver>();
        services.AddDisplayDriver<TicketPriority, TicketPriorityDisplayDriver>();
        services.AddDisplayDriver<TicketDesk, TicketDeskDisplayDriver>();
        services.AddDisplayDriver<TicketView, TicketViewDisplayDriver>();

        services.AddPermissionProvider<TicketsPermissionsProvider>();
        services.AddNavigationProvider<TicketsAdminMenu>();
    }
}

/// <summary>
/// Registers the recipe steps of the ticket settings.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TicketStatusesRecipeStep>();
        services.AddRecipeExecutionStep<TicketPrioritiesRecipeStep>();
        services.AddRecipeExecutionStep<TicketDesksRecipeStep>();
        services.AddRecipeExecutionStep<TicketViewsRecipeStep>();
    }
}

/// <summary>
/// Registers the deployment steps of the ticket settings.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TicketStatusesDeploymentSource, TicketStatusesDeploymentStep, TicketStatusesDeploymentStepDisplayDriver>();
        services.AddDeployment<TicketPrioritiesDeploymentSource, TicketPrioritiesDeploymentStep, TicketPrioritiesDeploymentStepDisplayDriver>();
        services.AddDeployment<TicketDesksDeploymentSource, TicketDesksDeploymentStep, TicketDesksDeploymentStepDisplayDriver>();
        services.AddDeployment<TicketViewsDeploymentSource, TicketViewsDeploymentStep, TicketViewsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the schemas of the ticket recipe and deployment steps.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TicketStatusesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, TicketPrioritiesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, TicketDesksRecipeStepSchema>();
        services.AddScoped<IRecipeStep, TicketViewsRecipeStepSchema>();
        services.AddDeploymentStepSchema<TicketStatusesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TicketPrioritiesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TicketDesksDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TicketViewsDeploymentStepSchema>();
    }
}
