using CrestApps.Core.Services;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the services and configuration for the base Contact Center feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterConstants.CollectionName));

        services
            .AddScoped<IInteractionStore, InteractionStore>()
            .AddScoped<IInteractionManager, InteractionManager>()
            .AddScoped<IInteractionEventStore, InteractionEventStore>()
            .AddScoped<IContactCenterOutboxStore, ContactCenterOutboxStore>()
            .AddScoped<IContactCenterOutbox, ContactCenterOutbox>()
            .AddScoped<IContactCenterEventPublisher, DefaultContactCenterEventPublisher>()
            .AddScoped<ICatalogEntryHandler<Interaction>, InteractionHandler>();

        services
            .AddScoped<ICallSessionStore, CallSessionStore>()
            .AddScoped<ICallSessionManager, CallSessionManager>();

        services
            .AddIndexProvider<InteractionIndexProvider>()
            .AddDataMigration<InteractionIndexMigrations>();

        services
            .AddIndexProvider<InteractionEventIndexProvider>()
            .AddDataMigration<InteractionEventIndexMigrations>();

        services
            .AddIndexProvider<ContactCenterOutboxMessageIndexProvider>()
            .AddDataMigration<ContactCenterOutboxMessageIndexMigrations>();

        services
            .AddIndexProvider<CallSessionIndexProvider>()
            .AddDataMigration<CallSessionIndexMigrations>();

        services.AddSingleton<IBackgroundTask, OutboxDispatchBackgroundTask>();
        services.AddPermissionProvider<ContactCenterPermissionProvider>();
    }
}

/// <summary>
/// Registers agent profiles, presence, capacity, skills, and queue/campaign sign-in.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class AgentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAgentProfileStore, AgentProfileStore>()
            .AddScoped<IAgentProfileManager, AgentProfileManager>()
            .AddScoped<IAgentPresenceManager, AgentPresenceManagerService>()
            .AddScoped<IAgentStateReasonCodeStore, AgentStateReasonCodeStore>()
            .AddScoped<IAgentStateReasonCodeManager, AgentStateReasonCodeManager>();

        services
            .AddIndexProvider<AgentProfileIndexProvider>()
            .AddDataMigration<AgentProfileIndexMigrations>();

        services
            .AddDisplayDriver<AgentStateReasonCode, AgentStateReasonCodeDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<AgentStateReasonCode>, AgentStateReasonCodeHandler>()
            .AddIndexProvider<AgentStateReasonCodeIndexProvider>()
            .AddDataMigration<AgentStateReasonCodeIndexMigrations>();

        services.AddNavigationProvider<ContactCenterAgentsAdminMenu>();
    }
}

/// <summary>
/// Registers recipe execution support for the agent feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class AgentsRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AgentStateReasonCodeStep>();
    }
}

/// <summary>
/// Registers queues, queue items, reservations, and availability-based assignment.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class QueuesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IActivityQueueStore, ActivityQueueStore>()
            .AddScoped<IActivityQueueManager, ActivityQueueManager>()
            .AddScoped<IContactCenterSkillStore, ContactCenterSkillStore>()
            .AddScoped<IContactCenterSkillManager, ContactCenterSkillManager>()
            .AddScoped<IBusinessHoursCalendarStore, BusinessHoursCalendarStore>()
            .AddScoped<IBusinessHoursCalendarManager, BusinessHoursCalendarManager>()
            .AddScoped<IBusinessHoursService, DefaultBusinessHoursService>()
            .AddScoped<IQueueItemStore, QueueItemStore>()
            .AddScoped<IQueueItemManager, QueueItemManager>()
            .AddScoped<IActivityReservationStore, ActivityReservationStore>()
            .AddScoped<IActivityReservationManager, ActivityReservationManager>()
            .AddScoped<IActivityQueueService, ActivityQueueService>()
            .AddScoped<IActivityReservationService, ActivityReservationService>()
            .AddScoped<IActivityRoutingService, ActivityRoutingService>()
            .AddScoped<IActivityRoutingStrategy, RequiredSkillsRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, CapacityRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, StickyAgentRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, LongestIdleRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, RoundRobinRoutingStrategy>()
            .AddScoped<IActivityRoutingStrategy, LeastBusyRoutingStrategy>()
            .AddScoped<IActivityAssignmentService, ActivityAssignmentService>()
            .AddScoped<ContactCenterAdminFormOptionsProvider>();

        services
            .AddDisplayDriver<ActivityQueue, ActivityQueueDisplayDriver>()
            .AddDisplayDriver<ContactCenterSkill, ContactCenterSkillDisplayDriver>()
            .AddDisplayDriver<BusinessHoursCalendar, BusinessHoursCalendarDisplayDriver>()
            .AddDisplayDriver<SoftPhoneWidget, ContactCenterSoftPhoneWidgetDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<ActivityQueue>, ActivityQueueHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterSkill>, ContactCenterSkillHandler>()
            .AddScoped<ICatalogEntryHandler<BusinessHoursCalendar>, BusinessHoursCalendarHandler>()
            .AddIndexProvider<ActivityQueueIndexProvider>()
            .AddDataMigration<ActivityQueueIndexMigrations>()
            .AddIndexProvider<ContactCenterSkillIndexProvider>()
            .AddDataMigration<ContactCenterSkillIndexMigrations>()
            .AddIndexProvider<BusinessHoursCalendarIndexProvider>()
            .AddDataMigration<BusinessHoursCalendarIndexMigrations>()
            .AddIndexProvider<QueueItemIndexProvider>()
            .AddDataMigration<QueueItemIndexMigrations>()
            .AddIndexProvider<ActivityReservationIndexProvider>()
            .AddDataMigration<ActivityReservationIndexMigrations>();

        services.AddSingleton<IBackgroundTask, ReservationExpiryBackgroundTask>();
        services.AddNavigationProvider<ContactCenterAdminMenu>();
    }
}

/// <summary>
/// Registers outbound dialing profiles, pacing, and dialer activity batch sources.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
public sealed class DialerStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public DialerStartup(IStringLocalizer<DialerStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IDialerProfileStore, DialerProfileStore>()
            .AddScoped<IDialerProfileManager, DialerProfileManager>()
            .AddScoped<IDialerService, DialerService>()
            .AddScoped<IDialerAttemptService, DialerAttemptService>()
            .AddScoped<IDialerEligibilityService, DefaultDialerEligibilityService>()
            .AddScoped<IDialerStrategyResolver, DialerStrategyResolver>()
            .AddScoped<IDialerStrategy, PowerDialerStrategy>()
            .AddScoped<IDialerStrategy, ProgressiveDialerStrategy>()
            .AddScoped<IDialerProviderResolver, DialerProviderResolver>();

        services
            .AddDisplayDriver<DialerProfile, DialerProfileDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<DialerProfile>, DialerProfileHandler>()
            .AddIndexProvider<DialerProfileIndexProvider>()
            .AddDataMigration<DialerProfileIndexMigrations>();

        services.AddSingleton<IBackgroundTask, DialerPacingBackgroundTask>();
        services.AddNavigationProvider<ContactCenterDialerAdminMenu>();

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.PreviewDial, entry =>
            {
                entry.DisplayName = S["Preview dial batch"];
                entry.Description = S["Loads unassigned activities the dialer offers to agents one at a time for review before dialing."];
                entry.RequiresUserAssignment = false;
            });

            options.AddSource(ActivitySources.PowerDial, entry =>
            {
                entry.DisplayName = S["Power dial batch"];
                entry.Description = S["Loads unassigned activities the dialer dials automatically for available agents."];
                entry.RequiresUserAssignment = false;
            });
        });
    }
}

/// <summary>
/// Registers the Voice Contact Center Call Router that routes inbound and outbound voice calls while
/// Telephony providers execute media operations.
/// </summary>
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class VoiceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IInboundContactLookup, InboundContactLookup>()
            .AddScoped<IContactCenterVoiceProviderResolver, ContactCenterVoiceProviderResolver>()
            .AddScoped<IContactCenterCallCommandService, ContactCenterCallCommandService>()
            .AddScoped<IProviderVoiceEventService, ProviderVoiceEventService>()
            .AddScoped<IProviderVoiceWebhookProcessor, ProviderVoiceWebhookProcessor>()
            .AddScoped<IContactCenterTransferService, ContactCenterTransferService>()
            .AddScoped<VoiceContactCenterCallRouter>()
            .AddScoped<IVoiceContactCenterCallRouter>(sp => sp.GetRequiredService<VoiceContactCenterCallRouter>())
            .AddScoped<IInboundVoiceService>(sp => sp.GetRequiredService<VoiceContactCenterCallRouter>())
            .AddScoped<IIncomingCallContextProvider, ContactCenterIncomingCallContextProvider>();
    }
}

/// <summary>
/// Registers the real-time agent and supervisor experience: the SignalR hub, the live agent session
/// store, the heartbeat-driven stale-session cleanup, and the event projection that broadcasts presence,
/// offer, and queue updates to connected clients.
/// </summary>
[Feature(ContactCenterConstants.Feature.RealTime)]
public sealed class RealTimeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAgentSessionStore, AgentSessionStore>()
            .AddScoped<IAgentSessionManager, AgentSessionManager>()
            .AddScoped<IAgentSessionService, AgentSessionService>()
            .AddScoped<IContactCenterRealTimeNotifier, ContactCenterRealTimeNotifier>()
            .AddScoped<IContactCenterEventHandler, ContactCenterRealTimeEventHandler>();

        services
            .AddIndexProvider<AgentSessionIndexProvider>()
            .AddDataMigration<AgentSessionIndexMigrations>();

        services.AddSingleton<IBackgroundTask, AgentSessionCleanupBackgroundTask>();
        services.AddResourceConfiguration<ContactCenterRealTimeResourceConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        HubRouteManager.MapHub<ContactCenterHub>(routes);
    }
}
