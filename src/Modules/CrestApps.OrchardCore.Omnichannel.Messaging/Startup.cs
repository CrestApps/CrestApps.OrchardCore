using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;
using CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Registers the Omnichannel Messaging workspace: the channel registry, the conversation catalog, the inbound
/// routing pipeline every channel feeds, the two-way send service, and the workspace UI. It registers no channel
/// itself; a channel feature (SMS, and later email, WhatsApp and the like) adds one with
/// <c>AddMessagingChannel&lt;TChannel&gt;()</c> and feeds its inbound traffic to <see cref="IMessagingInboundProcessor"/>.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Workspace tunables (thread lock waits, inbox page size, outbox batching).
        services.Configure<MessagingWorkspaceOptions>(_shellConfiguration.GetSection("CrestApps:Omnichannel:Messaging"));

        // The registry of the channels enabled on the tenant. Each channel feature adds its own.
        services.AddScoped<IMessagingChannelResolver, MessagingChannelResolver>();

        // Conversation catalog.
        services
            .AddScoped<IMessagingConversationStore, MessagingConversationStore>()
            .AddScoped<IMessagingConversationManager, MessagingConversationManager>();

        // Canned-response template catalog.
        services
            .AddScoped<IMessageTemplateStore, MessageTemplateStore>()
            .AddScoped<IMessageTemplateManager, MessageTemplateManager>()
            .AddScoped<ICatalogEntryHandler<MessageTemplate>, MessageTemplateHandler>();

        // Broadcast catalog + fan-out.
        services
            .AddScoped<IMessagingBroadcastStore, MessagingBroadcastStore>()
            .AddScoped<IMessagingBroadcastManager, MessagingBroadcastManager>()
            .AddScoped<IMessagingBroadcastService, MessagingBroadcastService>()
            .AddScoped<ICatalogEntryHandler<MessagingBroadcast>, MessagingBroadcastHandler>();

        // Two-way send, through whichever channel the conversation runs on.
        services.AddScoped<IMessagingConversationService, MessagingConversationService>();

        // Handing a conversation to another person or back to a team, and the names the workspace shows for agents.
        services
            .AddScoped<IMessagingConversationTransferService, MessagingConversationTransferService>()
            .AddScoped<IMessagingAgentNameProvider, MessagingAgentNameProvider>();

        // Per-thread authorization: the workspace permission grants the workspace, this decides which threads
        // inside it a caller owns or serves. The handler narrows the workspace permission when a conversation is
        // supplied as the authorization resource.
        services
            .AddScoped<IMessagingConversationAuthorizationService, MessagingConversationAuthorizationService>()
            .AddScoped<IAuthorizationHandler, MessagingConversationAuthorizationHandler>();

        // The handler declares the authorization service it narrows, but takes it lazily: that service consults
        // the authorization system, which is what runs the handler.
        services.AddScoped(sp => new Lazy<IMessagingConversationAuthorizationService>(sp.GetRequiredService<IMessagingConversationAuthorizationService>));

        // Inbound routing chain (deterministic order via IMessagingInboundRouter.Order). The routed (push) router
        // is contributed by the Routed Distribution feature, which owns the Work Distribution dependency.
        services
            .AddScoped<IMessagingInboundRouter, AutoReplyRouter>()
            .AddScoped<IMessagingInboundRouter, ReassignmentRouter>()
            .AddScoped<IMessagingInboundRouter, HandoffQueueRouter>()
            .AddScoped<IMessagingInboundRouter, ExistingConversationRouter>()
            .AddScoped<IMessagingInboundRouter, EndpointRouteRouter>()
            .AddScoped<IMessagingInboundRouter, FallbackRouter>();

        // The one entry point every ownership decision goes through, whatever triggered it.
        services.AddScoped<IMessagingConversationRouter, MessagingConversationRouter>();

        // The first-response clock and the pass that announces the threads that missed it.
        // A tenant with no Work Distribution has no queues, so the null reader reports every lookup as not
        // found and the SLA and quiet-hours paths take their no-policy branch. Work Distribution replaces it.
        services.TryAddScoped<IMessagingQueuePolicyReader, NullMessagingQueuePolicyReader>();
        services.AddScoped<IMessagingFirstResponseSlaService, MessagingFirstResponseSlaService>();

        // The contact time zone quiet hours are judged in, and the guard that reads it for the channels that
        // observe quiet hours.
        services.AddScoped<IMessagingContactTimeZoneResolver, MessagingContactTimeZoneResolver>();
        services.AddScoped<MessagingQuietHoursGuard>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, MessagingFirstResponseSlaBackgroundTask>());

        // The background run makes one pass and holds an in-process deadline for the next, instead of looping inside
        // its minute. The scheduler is the Contact Center one, shared with the queues feature when both are on.
        services.AddScoped<MessagingFirstResponseSlaSweep>();
        services.TryAddSingleton<IContactCenterDeadlineScheduler, ContactCenterDeadlineScheduler>();

        // Per-agent messaging availability is independent of voice presence, so it stays in the base feature: the
        // inbox toggle works whether or not push distribution is enabled.
        services.AddScoped<IMessagingAvailabilityService, MessagingAvailabilityService>();

        // Workspace presence lives in the distributed cache: a heartbeat is a fact that expires, and the cache is
        // shared across nodes wherever the deployment has configured it to be.
        services.AddSingleton<IMessagingPresenceTracker, DistributedCacheMessagingPresenceTracker>();

        // The inbound pipeline every channel's receiver hands its messages to.
        services.AddScoped<IMessagingInboundProcessor, MessagingInboundProcessor>();

        // Resolve the CRM contact behind a contact address, through the address's own channel. The contact content
        // types are read from the content definitions, so contact search works without the Omnichannel Management
        // administration.
        services.AddScoped<IMessagingContactResolver, MessagingContactResolver>();
        services.TryAddScoped<IOmnichannelContactTypeProvider, ContentDefinitionOmnichannelContactTypeProvider>();

        // Real-time messaging notifications over the workspace SignalR hub.
        services.AddScoped<IMessagingRealTimeNotifier, MessagingRealTimeNotifier>();

        // Receives AI-to-agent handoffs for every enabled messaging channel: moves an escalated automated
        // conversation into a queue-owned human thread in the inbox.
        // The escalation path asks the routing strategy whether a routed queue would push this thread to
        // someone. Without the routed-distribution feature the default selects nobody, so the thread is pooled,
        // which is the behaviour that tenant configured.
        services.TryAddScoped<IMessagingRoutingStrategy, NoMessagingRoutingStrategy>();
        services.AddScoped<IOmnichannelHandoffService, MessagingAgentHandoffService>();

        // Storage schema + indexes.
        // The workspace stores its catalog documents in a dedicated YesSql collection. Registering it makes
        // OrchardCore initialize the collection's "{prefix}_Document" table for the tenant.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(MessagingStorage.CollectionName));

        services.AddIndexProvider<MessagingConversationIndexProvider>();
        services.AddIndexProvider<MessageTemplateIndexProvider>();
        services.AddIndexProvider<MessagingBroadcastIndexProvider>();
        services.AddDataMigration<MessagingMigrations>();

        // Background fan-out for queued broadcasts, and the retry pass for outbound messages a provider refused.
        services.AddScoped<IMessagingOutbox, MessagingOutbox>();
        services.AddSingleton<IBackgroundTask, MessagingBroadcastBackgroundTask>();
        services.AddSingleton<IBackgroundTask, MessagingOutboxBackgroundTask>();

        // Admin surfaces. Inbound routing is edited on the channel-endpoint screen of every messaging channel's
        // endpoints (no separate routing catalog).
        // Endpoints of every messaging channel are stored in their channel's normalized form, so inbound traffic matches them.
        // The channel-endpoint handler of the Omnichannel feature applies it; the workspace only says how.
        services.AddScoped<IChannelEndpointAddressPolicy, MessagingChannelEndpointAddressPolicy>();
        services.AddDisplayDriver<OmnichannelChannelEndpoint, MessagingEndpointRoutingDisplayDriver>();
        services.AddDisplayDriver<MessagingConversation, MessagingConversationDisplayDriver>();
        services.AddDisplayDriver<MessageTemplate, MessageTemplateDisplayDriver>();
        services.AddNavigationProvider<MessagingAdminMenu>();

        // The workspace page's view assembly and the composer's contact search.
        services
            .AddScoped<MessagingWorkspaceBuilder>()
            .AddScoped<MessagingContactSearch>()
            .AddScoped<MessagingTransferTargets>();

        // Permissions.
        services.AddPermissionProvider<MessagingPermissionProvider>();

        // Redact contact/service addresses in logs, matching the other telephony modules.
        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<MessagingHub>(SignalRHubRoutes.GetHubPath<MessagingHub>());
    }
}
