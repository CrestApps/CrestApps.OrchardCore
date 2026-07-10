using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Reports;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the services and configuration for the base Contact Center feature.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind Contact Center options.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterConstants.CollectionName));

        services.Configure<ContactCenterRetentionOptions>(_shellConfiguration.GetSection("CrestApps_ContactCenter:Retention"));

        services
            .AddScoped<IInteractionStore, InteractionStore>()
            .AddScoped<IInteractionManager, InteractionManager>()
            .AddScoped<IInteractionEventStore, InteractionEventStore>()
            .AddScoped<IContactCenterOutboxStore, ContactCenterOutboxStore>()
            .AddScoped<IContactCenterOutbox, ContactCenterOutbox>()
            .AddScoped<IContactCenterEventPublisher, DefaultContactCenterEventPublisher>()
            .AddScoped<IContactCenterMetricStore, ContactCenterMetricStore>()
            .AddScoped<IContactCenterMetricsService, ContactCenterMetricsService>()
            .AddScoped<IContactCenterEventHandler, ContactCenterMetricsProjectionHandler>()
            .AddScoped<IContactCenterRetentionService, ContactCenterRetentionService>()
            .AddScoped<IContactCenterAssistService, ContactCenterAssistService>()
            .AddScoped<ICatalogEntryHandler<Interaction>, InteractionHandler>();

        services
            .AddIndexProvider<ContactCenterEventMetricIndexProvider>()
            .AddDataMigration<ContactCenterEventMetricIndexMigrations>();

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
        services.AddSingleton<IBackgroundTask, ContactCenterRetentionBackgroundTask>();
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

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.Use(async (context, next) =>
        {
            var isLogoutRequest = IsLogoutRequest(context);
            var userId = isLogoutRequest
                ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;
            var logger = isLogoutRequest
                ? context.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AgentsStartup>>()
                : null;

            if (isLogoutRequest)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Observed Orchard logout request for user '{UserId}'.", userId);
                }
            }

            await next();

            if (!isLogoutRequest || string.IsNullOrEmpty(userId) || context.Response.StatusCode >= 400)
            {
                return;
            }

            var presenceManager = context.RequestServices.GetRequiredService<IAgentPresenceManager>();
            await presenceManager.SignOutAsync(userId, context.RequestAborted);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Completed Contact Center logout synchronization for Orchard user '{UserId}' with response status {StatusCode}.",
                    userId,
                    context.Response.StatusCode);
            }
        });
    }

    private static bool IsLogoutRequest(HttpContext httpContext)
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method) || httpContext.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return string.Equals(httpContext.Request.Path.Value, "/Users/Account/LogOff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(httpContext.Request.Path.Value, "/Users/Account/Logout", StringComparison.OrdinalIgnoreCase);
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
            .AddScoped<IAgentWorkStateHealingService, AgentWorkStateHealingService>()
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
        services.AddResourceConfiguration<ContactCenterSoftPhoneResourceConfiguration>();
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
            .AddScoped<ICallbackRequestStore, CallbackRequestStore>()
            .AddScoped<ICallbackRequestManager, CallbackRequestManager>()
            .AddScoped<ICallbackService, CallbackService>()
            .AddScoped<IDialerService, DialerService>()
            .AddScoped<IDialerAttemptService, DialerAttemptService>()
            .AddScoped<IDialerEligibilityService, DefaultDialerEligibilityService>()
            .AddScoped<IDialerStrategyResolver, DialerStrategyResolver>()
            .AddScoped<IDialerStrategy, PowerDialerStrategy>()
            .AddScoped<IDialerStrategy, ProgressiveDialerStrategy>();

        services
            .AddDisplayDriver<DialerProfile, DialerProfileDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<DialerProfile>, DialerProfileHandler>()
            .AddIndexProvider<DialerProfileIndexProvider>()
            .AddDataMigration<DialerProfileIndexMigrations>()
            .AddIndexProvider<CallbackRequestIndexProvider>()
            .AddDataMigration<CallbackRequestIndexMigrations>();

        services.AddSingleton<IBackgroundTask, DialerPacingBackgroundTask>();
        services.AddSingleton<IBackgroundTask, CallbackDispatchBackgroundTask>();
        services.AddNavigationProvider<ContactCenterDialerAdminMenu>();

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Dialer, entry =>
            {
                entry.DisplayName = S["Dialer"];
                entry.Description = S["Loads unassigned activities and applies the selected dialer profile when the batch is loaded."];
                entry.RequiresUserAssignment = false;
            });

            options.AddSource(ActivitySources.PreviewDial, entry =>
            {
                entry.DisplayName = S["Preview dial batch"];
                entry.Description = S["Loads unassigned activities the dialer offers to agents one at a time for review before dialing."];
                entry.RequiresUserAssignment = false;
                entry.ShowInCreationPicker = false;
            });

            options.AddSource(ActivitySources.PowerDial, entry =>
            {
                entry.DisplayName = S["Power dial batch"];
                entry.Description = S["Loads unassigned activities the dialer dials automatically for available agents."];
                entry.RequiresUserAssignment = false;
                entry.ShowInCreationPicker = false;
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
            .AddScoped<IProviderCallStateSynchronizationService, ProviderCallStateSynchronizationService>()
            .AddScoped<IProviderVoiceEventService, ProviderVoiceEventService>()
            .AddScoped<IProviderVoiceOfferSynchronizationService, ProviderVoiceOfferSynchronizationService>()
            .AddScoped<IProviderVoiceWebhookProcessor, ProviderVoiceWebhookProcessor>()
            .AddScoped<IContactCenterTransferService, ContactCenterTransferService>()
            .AddScoped<IContactCenterRecordingService, ContactCenterRecordingService>()
            .AddScoped<IContactCenterMonitoringService, ContactCenterMonitoringService>()
            .AddScoped<IContactCenterEntryPointStore, ContactCenterEntryPointStore>()
            .AddScoped<IContactCenterEntryPointManager, ContactCenterEntryPointManager>()
            .AddScoped<IContactCenterEventHandler, ContactCenterSoftPhoneEventHandler>()
            .AddScoped<IContactCenterEventHandler, ContactCenterVoiceOfferReconciliationHandler>()
            .AddScoped<IQueuedVoiceWorkOfferService, QueuedVoiceWorkOfferService>()
            .AddScoped<IPendingIncomingCallOfferService, PendingIncomingCallOfferService>()
            .AddScoped<IEntryPointResolver, EntryPointResolver>()
            .AddScoped<VoiceContactCenterCallRouter>()
            .AddScoped<IVoiceContactCenterCallRouter>(sp => sp.GetRequiredService<VoiceContactCenterCallRouter>())
            .AddScoped<IInboundVoiceService>(sp => sp.GetRequiredService<VoiceContactCenterCallRouter>())
            .AddScoped<IIncomingCallContextProvider, ContactCenterIncomingCallContextProvider>()
            .AddScoped<IModularTenantEvents, ContactCenterVoiceTenantEvents>();

        services
            .AddDisplayDriver<ContactCenterEntryPoint, ContactCenterEntryPointDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterEntryPointHandler>()
            .AddScoped<IContactCenterEventHandler, OfferQueuedVoiceWorkOnAvailabilityHandler>()
            .AddIndexProvider<ContactCenterEntryPointIndexProvider>()
            .AddDataMigration<ContactCenterEntryPointIndexMigrations>();

        services.AddNavigationProvider<ContactCenterEntryPointsAdminMenu>();
        services.AddSingleton<IBackgroundTask, ProviderCallStateReconciliationBackgroundTask>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddVoiceOfferEndpoints()
            .AddVoiceIngressEndpoint()
            .AddProviderVoiceWebhookEndpoint();
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
        services.AddNavigationProvider<ContactCenterRealTimeAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        HubRouteManager.MapHub<ContactCenterHub>(routes);

        routes
            .AddAgentWorkspaceEndpoints()
            .AddSupervisorDashboardEndpoints();
    }
}

/// <summary>
/// Registers the reporting and analytics experience: the reporting service that aggregates interactions
/// and activities into productivity, call insights, queue usage, and campaign/subject progress reports,
/// and the Reports admin navigation.
/// </summary>
[Feature(ContactCenterConstants.Feature.Analytics)]
public sealed class AnalyticsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportingService, ContactCenterReportingService>();

        services
            .AddScoped<IReport, CallInsightsReportProvider>()
            .AddScoped<IReport, AgentProductivityReportProvider>()
            .AddScoped<IReport, QueueUsageReportProvider>()
            .AddScoped<IReport, CampaignSummaryReportProvider>()
            .AddScoped<IReport, SubjectInventoryReportProvider>();
    }
}

/// <summary>
/// Registers the optional OrchardCore Workflows bridge: a Contact Center workflow event activity and the
/// handler that triggers it for every published domain event.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ContactCenterWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ContactCenterEvent, ContactCenterEventDisplayDriver>();
        services.AddScoped<IContactCenterEventHandler, ContactCenterWorkflowEventHandler>();
    }
}
