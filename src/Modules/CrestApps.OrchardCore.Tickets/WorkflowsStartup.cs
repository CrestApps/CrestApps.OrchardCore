using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.PageSections;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Tickets.Drivers;
using CrestApps.OrchardCore.Tickets.Integrations;
using CrestApps.OrchardCore.Tickets.Recipes.Workflows;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.Workflows;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// The ticket workflow events and tasks, the named ticket actions and the time-elapsed scan.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketWorkflowObserver>());
        services.TryAddScoped<ITicketActionSource, WorkflowTicketActionSource>();
        services.AddScoped<TicketTimeElapsedScanner>();
        services.AddSingleton<IBackgroundTask, TicketTimeElapsedBackgroundTask>();

        services.AddActivity<TicketCreatedEvent, TicketEventDisplayDriver<TicketCreatedEvent>>();
        services.AddActivity<TicketUpdatedEvent, TicketEventDisplayDriver<TicketUpdatedEvent>>();
        services.AddActivity<TicketStatusChangedEvent, TicketEventDisplayDriver<TicketStatusChangedEvent>>();
        services.AddActivity<TicketAssignedEvent, TicketEventDisplayDriver<TicketAssignedEvent>>();
        services.AddActivity<TicketCustomerRepliedEvent, TicketEventDisplayDriver<TicketCustomerRepliedEvent>>();
        services.AddActivity<TicketAgentRepliedEvent, TicketEventDisplayDriver<TicketAgentRepliedEvent>>();
        services.AddActivity<TicketNoteAddedEvent, TicketEventDisplayDriver<TicketNoteAddedEvent>>();
        services.AddActivity<TicketServiceLevelWarningEvent, TicketEventDisplayDriver<TicketServiceLevelWarningEvent>>();
        services.AddActivity<TicketServiceLevelBreachedEvent, TicketEventDisplayDriver<TicketServiceLevelBreachedEvent>>();
        services.AddActivity<TicketSolvedEvent, TicketEventDisplayDriver<TicketSolvedEvent>>();
        services.AddActivity<TicketReopenedEvent, TicketEventDisplayDriver<TicketReopenedEvent>>();
        services.AddActivity<TicketClosedEvent, TicketEventDisplayDriver<TicketClosedEvent>>();
        services.AddActivity<TicketMergedEvent, TicketEventDisplayDriver<TicketMergedEvent>>();
        services.AddActivity<TicketActionRequestedEvent, TicketEventDisplayDriver<TicketActionRequestedEvent>>();
        services.AddActivity<TicketTimeElapsedEvent, TicketEventDisplayDriver<TicketTimeElapsedEvent>>();

        services.AddActivity<CreateTicketTask, TicketTaskDisplayDriver<CreateTicketTask>>();
        services.AddActivity<UpdateTicketTask, TicketTaskDisplayDriver<UpdateTicketTask>>();
        services.AddActivity<AssignTicketTask, TicketTaskDisplayDriver<AssignTicketTask>>();
        services.AddActivity<AddTicketReplyTask, TicketTaskDisplayDriver<AddTicketReplyTask>>();
        services.AddActivity<AddTicketNoteTask, TicketTaskDisplayDriver<AddTicketNoteTask>>();
        services.AddActivity<EscalateTicketTask, TicketTaskDisplayDriver<EscalateTicketTask>>();
        services.AddActivity<GetTicketTask, TicketTaskDisplayDriver<GetTicketTask>>();
        services.AddActivity<FindTicketsTask, TicketTaskDisplayDriver<FindTicketsTask>>();
        services.AddActivity<CheckTicketServiceLevelTask, TicketTaskDisplayDriver<CheckTicketServiceLevelTask>>();
        services.AddActivity<LinkTicketsTask, TicketTaskDisplayDriver<LinkTicketsTask>>();
        services.AddActivity<NotifyTicketTask, TicketTaskDisplayDriver<NotifyTicketTask>>();
    }
}

/// <summary>
/// The schemas of the ticket workflow activities.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<TicketCreatedEventSchema>()
            .AddWorkflowActivitySchema<TicketUpdatedEventSchema>()
            .AddWorkflowActivitySchema<TicketStatusChangedEventSchema>()
            .AddWorkflowActivitySchema<TicketAssignedEventSchema>()
            .AddWorkflowActivitySchema<TicketCustomerRepliedEventSchema>()
            .AddWorkflowActivitySchema<TicketAgentRepliedEventSchema>()
            .AddWorkflowActivitySchema<TicketNoteAddedEventSchema>()
            .AddWorkflowActivitySchema<TicketServiceLevelWarningEventSchema>()
            .AddWorkflowActivitySchema<TicketServiceLevelBreachedEventSchema>()
            .AddWorkflowActivitySchema<TicketSolvedEventSchema>()
            .AddWorkflowActivitySchema<TicketReopenedEventSchema>()
            .AddWorkflowActivitySchema<TicketClosedEventSchema>()
            .AddWorkflowActivitySchema<TicketMergedEventSchema>()
            .AddWorkflowActivitySchema<TicketActionRequestedEventSchema>()
            .AddWorkflowActivitySchema<TicketTimeElapsedEventSchema>()
            .AddWorkflowActivitySchema<CreateTicketTaskSchema>()
            .AddWorkflowActivitySchema<UpdateTicketTaskSchema>()
            .AddWorkflowActivitySchema<AssignTicketTaskSchema>()
            .AddWorkflowActivitySchema<AddTicketReplyTaskSchema>()
            .AddWorkflowActivitySchema<AddTicketNoteTaskSchema>()
            .AddWorkflowActivitySchema<EscalateTicketTaskSchema>()
            .AddWorkflowActivitySchema<GetTicketTaskSchema>()
            .AddWorkflowActivitySchema<FindTicketsTaskSchema>()
            .AddWorkflowActivitySchema<CheckTicketServiceLevelTaskSchema>()
            .AddWorkflowActivitySchema<LinkTicketsTaskSchema>()
            .AddWorkflowActivitySchema<NotifyTicketTaskSchema>();
    }
}

/// <summary>
/// Tells webhook subscribers about tickets.
/// </summary>
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketWebhookObserver>());
        services.AddWebhookEvent(TicketWebhookEvents.Created, "Ticket created", "Tickets");
        services.AddWebhookEvent(TicketWebhookEvents.Updated, "Ticket updated", "Tickets");
        services.AddWebhookEvent(TicketWebhookEvents.Assigned, "Ticket assigned", "Tickets");
        services.AddWebhookEvent(TicketWebhookEvents.ReplyAdded, "Ticket reply added", "Tickets");
        services.AddWebhookEvent(TicketWebhookEvents.Solved, "Ticket solved", "Tickets");
        services.AddWebhookEvent(TicketWebhookEvents.Closed, "Ticket closed", "Tickets");
    }
}

/// <summary>
/// Records ticket events in Orchard's audit trail.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AuditTrailStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, TicketAuditTrailEvents>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketAuditTrailObserver>());
    }
}

/// <summary>
/// Hands back the open tickets of a staff member who leaves.
/// </summary>
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffLifecycleObserver<TicketStaffLifecycleObserver>();
    }
}

/// <summary>
/// The Tickets tab of an account's page.
/// </summary>
[RequireFeatures(AccountsConstants.Feature)]
public sealed class AccountsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDisplayDriver, AccountTicketsTabDisplayDriver>();
        services.AddPageSection(AccountPageTabs.PageName, AccountPageTabs.Tickets, LocalizationSource.Create<AccountsStartup>("Tickets"), "fa-solid fa-ticket", AccountPageTabs.GetPosition(AccountPageTabs.Tickets));
    }
}
