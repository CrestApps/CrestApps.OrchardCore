using CrestApps.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.BackgroundTasks;
using CrestApps.OrchardCore.Sms.Workspace.Core;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routers;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routing;
using CrestApps.OrchardCore.Sms.Workspace.Drivers;
using CrestApps.OrchardCore.Sms.Workspace.Handlers;
using CrestApps.OrchardCore.Sms.Workspace.Hubs;
using CrestApps.OrchardCore.Sms.Workspace.Indexes;
using CrestApps.OrchardCore.Sms.Workspace.Migrations;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using CrestApps.OrchardCore.Sms.Workspace.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Configuration;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Sms.Workspace;

/// <summary>
/// Registers the SMS Communication Portal: the conversation and number-route catalogs, the per-number provider
/// dispatcher, the inbound routing pipeline (hooked onto the shared Omnichannel event bus), and the two-way
/// send service.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IStringLocalizer<Startup> stringLocalizer, IShellConfiguration shellConfiguration)
    {
        S = stringLocalizer;
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Routed (push) SMS distribution tunables, overridable via the CrestApps:Sms:RoutedDistribution section.
        services.Configure<SmsRoutedDistributionOptions>(
            _shellConfiguration.GetSection("CrestApps:Sms:RoutedDistribution"));

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

        // Inbound routing chain (deterministic order via ISmsInboundRouter.Order).
        services
            .AddScoped<ISmsInboundRouter, ExistingConversationRouter>()
            .AddScoped<ISmsInboundRouter, RoutedQueueRouter>()
            .AddScoped<ISmsInboundRouter, NumberRouteRouter>()
            .AddScoped<ISmsInboundRouter, FallbackRouter>();

        // Routed (push) queue distribution: agent selection, per-agent SMS availability, and the sweep that
        // returns unpicked routed conversations to their shared pool.
        services
            .AddScoped<ISmsRoutingStrategy, LeastLoadedSmsRoutingStrategy>()
            .AddScoped<ISmsAgentAvailabilityService, SmsAgentAvailabilityService>()
            .AddScoped<ISmsRoutedReassignmentService, SmsRoutedReassignmentService>();

        // The inbound processor is both the portal's orchestration service and an Omnichannel event handler, so
        // any provider webhook that raises SmsReceived feeds the human conversation pipeline.
        services.AddScoped<SmsInboundProcessor>();
        services.AddScoped<ISmsInboundProcessor>(sp => sp.GetRequiredService<SmsInboundProcessor>());
        services.AddScoped<IOmnichannelEventHandler>(sp => sp.GetRequiredService<SmsInboundProcessor>());

        // Resolve the CRM contact for a contact number so conversations link to the contact.
        services.AddScoped<ISmsContactResolver, SmsContactResolver>();

        // Real-time messaging notifications over the SMS portal SignalR hub.
        services.AddScoped<ISmsRealTimeNotifier, SmsRealTimeNotifier>();

        // Receives AI-to-agent handoffs for the SMS channel: moves an escalated automated conversation into a
        // queue-owned human thread in the inbox.
        services.AddScoped<IOmnichannelHandoffService, SmsAgentHandoffService>();

        // Storage schema + indexes.
        // The SMS portal stores its catalog documents in a dedicated YesSql collection. Registering it makes
        // OrchardCore initialize the collection's "{prefix}_Document" table for the tenant.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SmsWorkspaceStorage.CollectionName));

        services.AddIndexProvider<SmsConversationIndexProvider>();
        services.AddIndexProvider<SmsTemplateIndexProvider>();
        services.AddIndexProvider<SmsBroadcastIndexProvider>();
        services.AddDataMigration<SmsConversationMigrations>();

        // Background fan-out for queued broadcasts.
        services.AddSingleton<IBackgroundTask, SmsBroadcastBackgroundTask>();
        services.AddSingleton<IBackgroundTask, SmsRoutedReassignmentBackgroundTask>();

        // Admin surfaces. SMS routing is edited on the channel-endpoint screen (no separate routing catalog).
        // Register the SMS channel as a channel-endpoint source, and the SMS-specific endpoint editors
        // (provider dropdown + inbound routing). Both drivers target endpoints whose channel is SMS.
        services.AddChannelEndpointSource(OmnichannelConstants.Channels.Sms, source =>
        {
            source.DisplayName = S["SMS"];
            source.Description = S["A number that sends and receives text messages, handled by the SMS Workspace."];
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
        services.AddPermissionProvider<SmsWorkspacePermissionProvider>();

        // Redact contact/service addresses in logs, matching the other telephony modules.
        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<SmsPortalHub>(SignalRHubRoutes.GetHubPath<SmsPortalHub>());
    }
}
