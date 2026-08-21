using CrestApps.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services.Routers;
using CrestApps.OrchardCore.Telephony.Sms.Handlers;
using CrestApps.OrchardCore.Telephony.Sms.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Migrations;
using CrestApps.OrchardCore.Telephony.Sms.Notifications;
using CrestApps.OrchardCore.Telephony.Sms.Services;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
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

        // Pluggable defaults the portal features can override.
        services.TryAddScoped<ISmsContactResolver, NullSmsContactResolver>();
        services.TryAddScoped<ISmsRealTimeNotifier, NullSmsRealTimeNotifier>();

        // Storage schema + indexes.
        services.AddIndexProvider<SmsConversationIndexProvider>();
        services.AddIndexProvider<SmsNumberRouteIndexProvider>();
        services.AddDataMigration<SmsPortalMigrations>();

        // Permissions.
        services.AddPermissionProvider<TelephonySmsPermissionProvider>();

        // Redact customer/service addresses in logs, matching the other telephony modules.
        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));
    }
}
