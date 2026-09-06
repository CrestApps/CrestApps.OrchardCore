using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Drivers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Handlers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Hubs;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Indexes;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Migrations;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal;

/// <summary>
/// Registers the SMS Communication Portal: the conversation and number-route catalogs, the per-number provider
/// dispatcher, the inbound routing pipeline (hooked onto the shared Omnichannel event bus), and the two-way
/// send service.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(
        IStringLocalizer<Startup> stringLocalizer,
        IShellConfiguration shellConfiguration)
    {
        S = stringLocalizer;
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Portal tunables (thread lock waits, inbox page size). The configuration section is deliberately still
        // "CrestApps:Sms:Workspace" so an existing appsettings entry keeps binding; renaming it would fail silently.
        services.Configure<SmsPortalOptions>(_shellConfiguration.GetSection("CrestApps:Sms:Workspace"));

        // Conversation catalog.
        services
            .AddScoped<ISmsConversationStore, SmsConversationStore>()
            .AddScoped<ISmsConversationManager, SmsConversationManager>();

        // Canned-response template catalog.
        services
            .AddScoped<ISmsTemplateStore, SmsTemplateStore>()
            .AddScoped<ISmsTemplateManager, SmsTemplateManager>()
            .AddScoped<ICatalogEntryHandler<SmsTemplate>, SmsTemplateHandler>();

        // Broadcast catalog + fan-out.
        services
            .AddScoped<ISmsBroadcastStore, SmsBroadcastStore>()
            .AddScoped<ISmsBroadcastManager, SmsBroadcastManager>()
            .AddScoped<ISmsBroadcastService, SmsBroadcastService>()
            .AddScoped<ICatalogEntryHandler<SmsBroadcast>, SmsBroadcastHandler>();

        // Provider dispatch and two-way send.
        services
            .AddScoped<ISmsDispatcher, SmsDispatcher>()
            .AddScoped<ISmsConversationService, SmsConversationService>();

        // Per-thread authorization: the portal permission grants the workspace, this decides which threads inside
        // it a caller owns or serves. The handler narrows the portal permission when a conversation is supplied as
        // the authorization resource.
        services
            .AddScoped<ISmsConversationAuthorizationService, SmsConversationAuthorizationService>()
            .AddScoped<IAuthorizationHandler, SmsConversationAuthorizationHandler>();

        // The handler declares the authorization service it narrows, but takes it lazily: that service consults
        // the authorization system, which is what runs the handler.
        services.AddScoped(sp => new Lazy<ISmsConversationAuthorizationService>(sp.GetRequiredService<ISmsConversationAuthorizationService>));

        // Inbound routing chain (deterministic order via ISmsInboundRouter.Order). The routed (push) router is
        // contributed by the Routed Distribution feature, which owns the Work Distribution dependency.
        services
            .AddScoped<ISmsInboundRouter, AutoReplyRouter>()
            .AddScoped<ISmsInboundRouter, ReassignmentRouter>()
            .AddScoped<ISmsInboundRouter, HandoffQueueRouter>()
            .AddScoped<ISmsInboundRouter, ExistingConversationRouter>()
            .AddScoped<ISmsInboundRouter, NumberRouteRouter>()
            .AddScoped<ISmsInboundRouter, FallbackRouter>();

        // The one entry point every ownership decision goes through, whatever triggered it.
        services.AddScoped<ISmsConversationRouter, SmsConversationRouter>();

        // The first-response clock and the pass that announces the threads that missed it.
        // A tenant with no Work Distribution has no queues, so the null reader reports every lookup as not
        // found and the SLA and quiet-hours paths take their no-policy branch. Work Distribution replaces it.
        services.TryAddScoped<ISmsQueuePolicyReader, NullSmsQueuePolicyReader>();
        services.AddScoped<ISmsFirstResponseSlaService, SmsFirstResponseSlaService>();

        // Carrier keywords, the contact time zone quiet hours are judged in, and the guard that reads both.
        services.Configure<SmsKeywordReplySettings>(_shellConfiguration.GetSection("CrestApps:Sms:Portal:KeywordReplies"));
        services.AddScoped<ISmsContactTimeZoneResolver, SmsContactTimeZoneResolver>();
        services.AddScoped<SmsQuietHoursGuard>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SmsFirstResponseSlaBackgroundTask>());

        // Per-agent SMS availability is independent of voice presence, so it stays in the base feature: the
        // inbox toggle works whether or not push distribution is enabled.
        services.AddScoped<ISmsAgentAvailabilityService, SmsAgentAvailabilityService>();

        // Portal presence lives in the distributed cache: a heartbeat is a fact that expires, and the cache is
        // shared across nodes wherever the deployment has configured it to be.
        services.AddSingleton<ISmsAgentPresenceTracker, DistributedCacheSmsAgentPresenceTracker>();

        // The inbound processor is both the portal's orchestration service and an Omnichannel event handler, so
        // any provider webhook that raises SmsReceived feeds the human conversation pipeline.
        services.AddScoped<SmsInboundProcessor>();
        services.AddScoped<ISmsInboundProcessor>(sp => sp.GetRequiredService<SmsInboundProcessor>());
        services.AddScoped<IOmnichannelEventHandler>(sp => sp.GetRequiredService<SmsInboundProcessor>());

        // Inbound SMS is committed to the durable provider webhook inbox before it is processed, keyed on the
        // provider's own message id, so a redelivered text is absorbed rather than stored and answered twice.
        services.AddScoped<IProviderWebhookInboxHandler, SmsInboundInboxHandler>();

        // Resolve the CRM contact for a contact number so conversations link to the contact. The contact content
        // types are read from the content definitions, so contact search works without the Omnichannel Management
        // administration.
        services.AddScoped<ISmsContactResolver, SmsContactResolver>();
        services.TryAddScoped<IOmnichannelContactTypeProvider, ContentDefinitionOmnichannelContactTypeProvider>();

        // Real-time messaging notifications over the SMS portal SignalR hub.
        services.AddScoped<ISmsRealTimeNotifier, SmsRealTimeNotifier>();

        // Receives AI-to-agent handoffs for the SMS channel: moves an escalated automated conversation into a
        // queue-owned human thread in the inbox.
        // The escalation path asks the routing strategy whether a routed queue would push this thread to
        // someone. Without the routed-distribution feature the default selects nobody, so the thread is pooled,
        // which is the behaviour that tenant configured.
        services.TryAddScoped<ISmsRoutingStrategy, NoSmsRoutingStrategy>();
        services.AddScoped<IOmnichannelHandoffService, SmsAgentHandoffService>();

        // Storage schema + indexes.
        // The SMS portal stores its catalog documents in a dedicated YesSql collection. Registering it makes
        // OrchardCore initialize the collection's "{prefix}_Document" table for the tenant.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SmsPortalStorage.CollectionName));

        services.AddIndexProvider<SmsConversationIndexProvider>();
        services.AddIndexProvider<SmsTemplateIndexProvider>();
        services.AddIndexProvider<SmsBroadcastIndexProvider>();
        services.AddDataMigration<SmsConversationMigrations>();

        // Background fan-out for queued broadcasts, and the retry pass for outbound messages a provider refused.
        services.AddScoped<ISmsOutboundOutbox, SmsOutboundOutbox>();
        services.AddSingleton<IBackgroundTask, SmsBroadcastBackgroundTask>();
        services.AddSingleton<IBackgroundTask, SmsOutboundOutboxBackgroundTask>();

        // Admin surfaces. SMS routing is edited on the channel-endpoint screen (no separate routing catalog).
        // Register the SMS channel as a channel-endpoint source, and the SMS-specific endpoint editors
        // (provider dropdown + inbound routing). Both drivers target endpoints whose channel is SMS.
        services.AddChannelEndpointSource(OmnichannelConstants.Channels.Sms, source =>
        {
            source.DisplayName = S["SMS"];
            source.Description = S["A number that sends and receives text messages, handled by the SMS Portal."];
        });

        services.AddDisplayDriver<OmnichannelChannelEndpoint, SmsEndpointProviderDisplayDriver>();
        services.AddDisplayDriver<OmnichannelChannelEndpoint, SmsEndpointRoutingDisplayDriver>();
        services.AddDisplayDriver<SmsConversation, SmsConversationDisplayDriver>();
        services.AddDisplayDriver<SmsTemplate, SmsTemplateDisplayDriver>();
        services.AddNavigationProvider<SmsPortalAdminMenu>();

        // Adds a "Send SMS" button next to phone-number fields on admin pages (mirrors the soft-phone dial button
        // and its PhoneFieldDialerShapeTableProvider). Injected from the phone field's own rendering via the shape
        // table, so only pages that actually show a phone field pay for it.
        services.AddShapeTableProvider<SmsPhoneFieldButtonShapeTableProvider>();

        // Permissions.
        services.AddPermissionProvider<SmsPortalPermissionProvider>();

        // Redact contact/service addresses in logs, matching the other telephony modules.
        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<SmsPortalHub>(SignalRHubRoutes.GetHubPath<SmsPortalHub>());
    }
}
