using CrestApps.Core.Hosting.Background;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;

/// <summary>
/// Registers the SMS portal's services, independently of the host they run in.
/// </summary>
public static class SmsPortalServiceCollectionExtensions
{
    /// <summary>
    /// Adds conversations, templates, broadcasts, inbound routing and the work that chases a first reply.
    /// </summary>
    /// <remarks>
    /// What stays with the host is what only a host can answer: where its documents are stored, how its
    /// indexes and migrations are registered, its administration screens, and the scheduler that drives the
    /// cycles added here.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration the portal's options are bound against.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreSmsPortal(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Portal tunables (thread lock waits, inbox page size). The configuration section is deliberately still
        // the workspace one so an existing entry keeps binding; renaming it would fail silently.
        services.Configure<SmsPortalOptions>(configuration.GetSection("CrestApps:Sms:Workspace"));

        // Conversation catalog.
        services
            .AddScoped<ISmsConversationStore, SmsConversationStore>()
            .AddScoped<ISmsConversationManager, SmsConversationManager>();

        // Canned-response template catalog.
        services
            .AddScoped<ISmsTemplateStore, SmsTemplateStore>()
            .AddScoped<ISmsTemplateManager, SmsTemplateManager>();

        // Broadcast catalog and fan-out.
        services
            .AddScoped<ISmsBroadcastStore, SmsBroadcastStore>()
            .AddScoped<ISmsBroadcastManager, SmsBroadcastManager>()
            .AddScoped<ISmsBroadcastService, SmsBroadcastService>();

        // Provider dispatch and two-way send.
        services
            .AddScoped<ISmsDispatcher, SmsDispatcher>()
            .AddScoped<ISmsConversationService, SmsConversationService>();

        // Per-thread authorization: the portal grant opens the workspace, this decides which threads inside it
        // a caller owns or serves.
        services.AddScoped<ISmsConversationAuthorizationService, SmsConversationAuthorizationService>();

        // Inbound routing chain. The order they run in is their own, declared on each router, and the routed
        // (push) router is contributed separately by whoever owns work distribution.
        services
            .AddScoped<ISmsInboundRouter, AutoReplyRouter>()
            .AddScoped<ISmsInboundRouter, ReassignmentRouter>()
            .AddScoped<ISmsInboundRouter, HandoffQueueRouter>()
            .AddScoped<ISmsInboundRouter, ExistingConversationRouter>()
            .AddScoped<ISmsInboundRouter, NumberRouteRouter>()
            .AddScoped<ISmsInboundRouter, FallbackRouter>();

        // The one entry point every ownership decision goes through, whatever triggered it.
        services.AddScoped<ISmsConversationRouter, SmsConversationRouter>();

        // The first-response clock and the pass that announces the threads that missed it. A host with no work
        // distribution has no queues, so the null reader reports every lookup as not found and the service-level
        // and quiet-hours paths take their no-policy branch.
        services.TryAddScoped<ISmsQueuePolicyReader, NullSmsQueuePolicyReader>();
        services.AddScoped<ISmsFirstResponseSlaService, SmsFirstResponseSlaService>();

        // Carrier keywords and the guard that reads them against the contact time zone quiet hours are
        // judged in. Which time zone a contact is in is the host's to answer, so it registers that.
        services.Configure<SmsKeywordReplySettings>(configuration.GetSection("CrestApps:Sms:Portal:KeywordReplies"));
        services.AddScoped<SmsQuietHoursGuard>();
        services.AddBackgroundCycle<ISmsFirstResponseSlaCycle, SmsFirstResponseSlaCycle>();

        // Per-agent SMS availability is independent of voice presence, so it belongs here: the inbox toggle
        // works whether or not push distribution is enabled.
        services.AddScoped<ISmsAgentAvailabilityService, SmsAgentAvailabilityService>();

        // Portal presence lives in the distributed cache: a heartbeat is a fact that expires, and the cache is
        // shared across nodes wherever the deployment has configured it to be.
        services.AddSingleton<ISmsAgentPresenceTracker, DistributedCacheSmsAgentPresenceTracker>();

        // The inbound processor is both the portal's orchestration service and an event handler, so any
        // provider webhook that reports a received message feeds the human conversation pipeline.
        services.AddScoped<SmsInboundProcessor>();
        services.AddScoped<ISmsInboundProcessor>(sp => sp.GetRequiredService<SmsInboundProcessor>());
        services.AddScoped<IOmnichannelEventHandler>(sp => sp.GetRequiredService<SmsInboundProcessor>());

        // Inbound messages are committed to the durable provider webhook inbox before they are processed, keyed
        // on the provider's own message id, so a redelivered text is absorbed rather than stored and answered
        // twice.
        services.AddScoped<IProviderWebhookInboxHandler, SmsInboundInboxHandler>();

        // Receives escalations for this channel: moves an automated conversation into a queue-owned human
        // thread. The escalation path asks the routing strategy whether a routed queue would push the thread at
        // somebody; without push distribution the default selects nobody, so the thread is pooled.
        services.TryAddScoped<ISmsRoutingStrategy, NoSmsRoutingStrategy>();
        services.AddScoped<IOmnichannelHandoffService, SmsAgentHandoffService>();

        services.AddScoped<ISmsOutboundOutbox, SmsOutboundOutbox>();

        return services;
    }
}
