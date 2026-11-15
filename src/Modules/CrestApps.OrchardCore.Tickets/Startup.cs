using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tickets.BackgroundTasks;
using CrestApps.OrchardCore.Tickets.Core;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Deployments;
using CrestApps.OrchardCore.Tickets.Integrations;
using CrestApps.OrchardCore.Tickets.Drivers;
using CrestApps.OrchardCore.Tickets.Migrations;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Navigation;
using CrestApps.OrchardCore.Tickets.Recipes;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

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

        services.AddDataMigration<TicketsMigrations>()
            .AddIndexProvider<TicketIndexProvider>()
            .AddIndexProvider<TicketBacklogIndexProvider>()
            .AddIndexProvider<TicketTimeElapsedMarkIndexProvider>()
            .AddIndexProvider<TicketEmailOutboxIndexProvider>()
            .AddIndexProvider<TicketLinkIndexProvider>();

        services.AddTicketsCore();
        services.AddScoped<IContentHandler, TicketContentCleanup>();
        services.AddTimelineAccessProvider<TicketTimelineAccess>();
        services.AddRecordSource<TicketRecordSource>();
        services.AddPersonalDataContributor<TicketPersonalDataContributor>();
        services.AddContentPart<TicketPart>()
            .UseDisplayDriver<TicketPartDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, TicketPartSettingsDisplayDriver>();
        services.AddScoped<TicketUploads>();
        services.AddDisplayDriver<TicketRow, TicketRowDisplayDriver>();
        services.AddSingleton<IBackgroundTask, TicketAutoCloseBackgroundTask>();
        services.AddSingleton<IBackgroundTask, TicketBacklogBackgroundTask>();
        services.AddSingleton<IBackgroundTask, TicketEmailOutboxBackgroundTask>();

        services
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Received, LocalizationSource.Create<Startup>("Ticket received"))
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Reply, LocalizationSource.Create<Startup>("Reply from an agent"), LocalizationSource.Create<Startup>("{{ Reply.Body }}, {{ Reply.AuthorName }}"))
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Solved, LocalizationSource.Create<Startup>("Ticket solved"))
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Rating, LocalizationSource.Create<Startup>("Rate a solved ticket"));
        services.AddSiteDisplayDriver<TicketEmailSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(TicketEmailSettingsDisplayDriver.GroupId, TicketsPermissions.ManageTicketSettings);

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
        services.AddScoped<IAuthorizationHandler, TicketContentAuthorizationHandler>();
        services.AddNavigationProvider<TicketsAdminMenu>();
    }
}

/// <summary>
/// Offers Messaging's reply templates of the Tickets scope in the ticket composer, with what using one changes on the
/// ticket.
/// </summary>
[RequireFeatures(MessagingConstants.Feature.Workspace)]
public sealed class MessagingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMessageTemplateScope(TicketsConstants.ReplyTemplateScope, LocalizationSource.Create<MessagingStartup>("Tickets"));
        services.TryAddScoped<ITicketReplyTemplateSource, MessagingTicketReplyTemplateSource>();
        services.AddDisplayDriver<MessageTemplate, TicketMacroDisplayDriver>();
    }
}

/// <summary>
/// Tells agents through Orchard's notifications when a ticket is assigned to them.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketAssignmentNotifier>());
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
