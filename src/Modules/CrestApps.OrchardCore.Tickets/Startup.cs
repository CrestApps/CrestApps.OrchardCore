using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentFields.Conditions;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tickets.BackgroundTasks;
using CrestApps.OrchardCore.Tickets.Categories;
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
using CrestApps.OrchardCore.Tickets.Notifications;
using CrestApps.OrchardCore.Tickets.Portal;
using CrestApps.OrchardCore.Tickets.RealTime;
using CrestApps.OrchardCore.Tickets.Recipes;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
using OrchardCore.Notifications;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
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

        // The service desk's own records live in their collection; tickets, as content items, stay in the default one.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TicketsConstants.CollectionName));

        services.AddDataMigration<TicketsMigrations>()
            .AddIndexProvider<TicketSettingsRecordIndexProvider<TicketDesk, TicketDeskIndex>>()
            .AddIndexProvider<TicketSettingsRecordIndexProvider<TicketStatus, TicketStatusIndex>>()
            .AddIndexProvider<TicketSettingsRecordIndexProvider<TicketPriority, TicketPriorityIndex>>()
            .AddIndexProvider<TicketSettingsRecordIndexProvider<TicketView, TicketViewIndex>>()
            .AddIndexProvider<TicketSettingsRecordIndexProvider<TicketBoard, TicketBoardIndex>>()
            .AddIndexProvider<TicketIndexProvider>()
            .AddIndexProvider<TicketBacklogIndexProvider>()
            .AddIndexProvider<TicketTimeElapsedMarkIndexProvider>()
            .AddIndexProvider<TicketEmailOutboxIndexProvider>()
            .AddIndexProvider<TicketProtectedItemIndexProvider>()
            .AddIndexProvider<TicketLinkIndexProvider>();

        services.AddTicketsCore();
        services.AddScoped<IContentHandler, TicketContentCleanup>();
        services.AddScoped<IContentHandler, TicketCategoryTaxonomyHandler>();
        services.AddTimelineAccessProvider<TicketTimelineAccess>();
        services.AddRecordSource<TicketRecordSource>();
        services.AddPersonalDataContributor<TicketPersonalDataContributor>();
        services.AddContentPart<TicketPart>()
            .UseDisplayDriver<TicketPartDisplayDriver>()
            .UseDisplayDriver<TicketPlanningDisplayDriver>();
        services.AddContentPart<TicketCategoryPart>()
            .UseDisplayDriver<TicketCategoryPartDisplayDriver>();

        // The category as a source of the type's field rules, and the fields customers don't fill in as hidden fields.
        services.AddScoped<IContentConditionSource, TicketCategoryConditionSource>();
        services.AddScoped<IContentFieldVisibilityProvider, TicketCustomerFieldsVisibility>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, TicketPartSettingsDisplayDriver>();
        services.AddScoped<TicketUploads>();
        services.AddScoped<TicketPresence>();
        services.AddDisplayDriver<TicketRow, TicketRowDisplayDriver>();
        services.AddSingleton<IBackgroundTask, TicketAutoCloseBackgroundTask>();
        services.AddSingleton<IBackgroundTask, TicketProblemIncidentsBackgroundTask>();
        services.AddSingleton<IBackgroundTask, TicketBacklogBackgroundTask>();
        services.AddSingleton<IBackgroundTask, TicketEmailOutboxBackgroundTask>();

        services
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Received, LocalizationSource.Create<Startup>("Ticket received"))
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Reply, LocalizationSource.Create<Startup>("Reply from an agent"), LocalizationSource.Create<Startup>("{{ Reply.Body }}, {{ Reply.AuthorName }}"))
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Solved, LocalizationSource.Create<Startup>("Ticket solved"))
            .AddTemplatedEmailKind(TicketsConstants.EmailGroup, TicketsConstants.EmailKinds.Rating, LocalizationSource.Create<Startup>("Rate a solved ticket"));
        // Settings > Service Desk: the notification templates, and the AI profile for replies with AI on.
        services.AddSiteDisplayDriver<TicketEmailSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(TicketSettingsAdminMenu.GroupId, TicketsPermissions.ManageTicketSettings);
        services.AddNavigationProvider<TicketSettingsAdminMenu>();

        // The settings records' rules come with the feature that imports them.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketStatus>, TicketSettingsRecordHandler<TicketStatus>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketPriority>, TicketSettingsRecordHandler<TicketPriority>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketDesk>, TicketSettingsRecordHandler<TicketDesk>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketView>, TicketSettingsRecordHandler<TicketView>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketBoard>, TicketSettingsRecordHandler<TicketBoard>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TicketBoard>, TicketBoardHandler>());

        services.AddDisplayDriver<TicketStatus, TicketStatusDisplayDriver>();
        services.AddDisplayDriver<TicketPriority, TicketPriorityDisplayDriver>();
        services.AddDisplayDriver<TicketDesk, TicketDeskDisplayDriver>();
        services.AddDisplayDriver<TicketDesk, TicketDeskPriorityMatrixDisplayDriver>();
        services.AddDisplayDriver<TicketView, TicketViewDisplayDriver>();
        services.AddDisplayDriver<TicketBoard, TicketBoardDisplayDriver>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, TicketsResourceConfiguration>();

        services.AddPermissionProvider<TicketsPermissionsProvider>();
        services.AddScoped<IAuthorizationHandler, TicketContentAuthorizationHandler>();
        services.TryAddScoped<ITicketBoardPins, UserTicketBoardPins>();
        services.TryAddScoped<ITicketPasswordConfirmation, TicketPasswordConfirmation>();
        services.TryAddScoped<TicketProtectedReveals>();
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
/// Tells people about their tickets through Orchard's notifications: the notification center and the methods each user
/// chose for each type in their notification preferences.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<TicketNotificationSender>();

        // A requester who has a site account gets the requester messages as notifications instead of e-mails.
        services.TryAddScoped<ITicketRequesterNotifier, TicketRequesterNotifier>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketAssignmentNotifier>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketNotificationObserver>());
        services.AddDisplayDriver<Notification, TicketNotificationDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<INotificationVisibilityHandler, TicketNotificationVisibilityHandler>());

        services.AddNotificationType(
            TicketsConstants.NotificationTypes.Assigned,
            LocalizationSource.Create<Startup>("Tickets assigned to you"),
            type => type.Description = LocalizationSource.Create<Startup>("When a ticket is given to you by someone else or by the desk's assignment."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.TeamAssigned,
            LocalizationSource.Create<Startup>("Tickets given to your team"),
            type => type.Description = LocalizationSource.Create<Startup>("When a ticket is given to a team you are on (the agent it is assigned to hears of it as theirs)."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.RequesterReplied,
            LocalizationSource.Create<Startup>("Replies from requesters"),
            type => type.Description = LocalizationSource.Create<Startup>("When the requester replies on your ticket, or on your team's unassigned ticket."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.NoteAdded,
            LocalizationSource.Create<Startup>("Internal notes on your tickets"),
            type => type.Description = LocalizationSource.Create<Startup>("When someone else adds an internal note to your ticket."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.StatusChanged,
            LocalizationSource.Create<Startup>("Status changes on your tickets"),
            type => type.Description = LocalizationSource.Create<Startup>("When someone else, or a workflow, moves your ticket to another status."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.ServiceLevel,
            LocalizationSource.Create<Startup>("Service-level targets"),
            type => type.Description = LocalizationSource.Create<Startup>("When a target of your ticket (or your team's unassigned ticket) is due soon or missed."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.Merged,
            LocalizationSource.Create<Startup>("Tickets merged into yours"),
            type => type.Description = LocalizationSource.Create<Startup>("When another ticket is merged into your ticket."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.Replied,
            LocalizationSource.Create<Startup>("Replies to your requests"),
            type => type.Description = LocalizationSource.Create<Startup>("When an agent replies on a request you sent."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.RequestReceived,
            LocalizationSource.Create<Startup>("Your requests received"),
            type => type.Description = LocalizationSource.Create<Startup>("When a request you sent is received, with its number."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.RequestSolved,
            LocalizationSource.Create<Startup>("Your requests solved"),
            type => type.Description = LocalizationSource.Create<Startup>("When a request you sent is solved."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.RatingRequested,
            LocalizationSource.Create<Startup>("Rating your solved requests"),
            type => type.Description = LocalizationSource.Create<Startup>("When you are asked how a solved request of yours was handled."));
        services.AddNotificationType(
            TicketsConstants.NotificationTypes.Workflow,
            LocalizationSource.Create<Startup>("Ticket workflow notices"),
            type => type.Description = LocalizationSource.Create<Startup>("When a workflow's Notify About Ticket task tells you about a ticket."));
    }
}

/// <summary>
/// Pings open ticket boards over SignalR when tickets change, so they reload at once; the boards still poll every 20
/// seconds when they aren't connected.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.SignalR)]
public sealed class RealTimeStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TicketBoardNotifier>();
        services.AddScoped<ITicketObserver>(serviceProvider => serviceProvider.GetRequiredService<TicketBoardNotifier>());
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapHub<TicketBoardHub>(SignalRHubRoutes.GetHubPath<TicketBoardHub>());
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
        services.AddRecipeExecutionStep<TicketBoardsRecipeStep>();
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
        services.AddDeployment<TicketBoardsDeploymentSource, TicketBoardsDeploymentStep, TicketBoardsDeploymentStepDisplayDriver>();
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
        services.AddScoped<IRecipeStep, TicketBoardsRecipeStepSchema>();
        services.AddDeploymentStepSchema<TicketStatusesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TicketPrioritiesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TicketDesksDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TicketViewsDeploymentStepSchema>();
        services.AddDeploymentStepSchema<TicketBoardsDeploymentStepSchema>();
    }
}
