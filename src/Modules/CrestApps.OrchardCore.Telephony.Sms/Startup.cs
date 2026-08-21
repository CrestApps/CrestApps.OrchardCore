using CrestApps.Core.Services;
using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Sms.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services.Routers;
using CrestApps.OrchardCore.Telephony.Sms.Drivers;
using CrestApps.OrchardCore.Telephony.Sms.Handlers;
using CrestApps.OrchardCore.Telephony.Sms.Hubs;
using CrestApps.OrchardCore.Telephony.Sms.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Migrations;
using CrestApps.OrchardCore.Telephony.Sms.Notifications;
using CrestApps.OrchardCore.Telephony.Sms.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Telephony.Sms;

/// <summary>
/// Registers the SMS Communication Portal: the conversation and number-route catalogs, the per-number provider
/// dispatcher, the inbound routing pipeline (hooked onto the shared Omnichannel event bus), and the two-way
/// send service.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Conversation catalog.
        services
            .AddScoped<ISmsConversationStore, SmsConversationStore>()
            .AddScoped<ISmsConversationManager, SmsConversationManager>();

        // Number-route catalog.
        services
            .AddScoped<ISmsNumberRouteStore, SmsNumberRouteStore>()
            .AddScoped<ISmsNumberRouteManager, SmsNumberRouteManager>()
            .AddScoped<ICatalogEntryHandler<SmsNumberRoute>, SmsNumberRouteHandler>();

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
            .AddScoped<ISmsInboundRouter, NumberRouteRouter>()
            .AddScoped<ISmsInboundRouter, FallbackRouter>();

        // The inbound processor is both the portal's orchestration service and an Omnichannel event handler, so
        // any provider webhook that raises SmsReceived feeds the human conversation pipeline.
        services.AddScoped<SmsInboundProcessor>();
        services.AddScoped<ISmsInboundProcessor>(sp => sp.GetRequiredService<SmsInboundProcessor>());
        services.AddScoped<IOmnichannelEventHandler>(sp => sp.GetRequiredService<SmsInboundProcessor>());

        // Pluggable contact resolution (a phone-match resolver can override the no-op default).
        services.TryAddScoped<ISmsContactResolver, NullSmsContactResolver>();

        // Real-time messaging notifications over the SMS portal SignalR hub.
        services.AddScoped<ISmsRealTimeNotifier, SmsRealTimeNotifier>();

        // Storage schema + indexes.
        // The SMS portal stores its catalog documents in a dedicated YesSql collection. Registering it makes
        // OrchardCore initialize the collection's "{prefix}_Document" table for the tenant.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TelephonySmsStorage.CollectionName));

        services.AddIndexProvider<SmsConversationIndexProvider>();
        services.AddIndexProvider<SmsNumberRouteIndexProvider>();
        services.AddIndexProvider<SmsTemplateIndexProvider>();
        services.AddIndexProvider<SmsBroadcastIndexProvider>();
        services.AddDataMigration<SmsPortalMigrations>();

        // Background fan-out for queued broadcasts.
        services.AddSingleton<IBackgroundTask, SmsBroadcastBackgroundTask>();

        // Admin surfaces.
        services.AddDisplayDriver<SmsNumberRoute, SmsNumberRouteDisplayDriver>();
        services.AddDisplayDriver<SmsConversation, SmsConversationDisplayDriver>();
        services.AddDisplayDriver<SmsTemplate, SmsTemplateDisplayDriver>();
        services.AddSiteDisplayDriver<SmsPortalSettingsDisplayDriver>();
        services.AddNavigationProvider<SmsPortalAdminMenu>();

        // Permissions.
        services.AddPermissionProvider<TelephonySmsPermissionProvider>();

        // Redact customer/service addresses in logs, matching the other telephony modules.
        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<SmsPortalHub>(SignalRHubRoutes.GetHubPath<SmsPortalHub>());
    }
}
