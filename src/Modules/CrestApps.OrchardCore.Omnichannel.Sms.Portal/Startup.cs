using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Data;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement;
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
        services.AddCoreHostSeams();
        services.AddCoreSmsProviderSeam();

        services.AddCoreSmsPortal(_shellConfiguration);

        services
            .AddScoped<ICatalogEntryHandler<SmsTemplate>, SmsTemplateHandler>()
            .AddScoped<ICatalogEntryHandler<SmsBroadcast>, SmsBroadcastHandler>();

        // Per-thread authorization narrows the portal grant when a conversation is supplied as the
        // authorization resource. The handler takes the service it narrows lazily: that service consults the
        // authorization system, which is what runs the handler.
        services
            .AddSmsPortalOperationAuthorization()
            .AddScoped<IAuthorizationHandler, SmsConversationAuthorizationHandler>();

        services.AddScoped(sp => new Lazy<ISmsConversationAuthorizationService>(sp.GetRequiredService<ISmsConversationAuthorizationService>));

        // The host's scheduler for the first-response cycle AddCoreSmsPortal registered.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SmsFirstResponseSlaBackgroundTask>());

        // Which time zone a contact is in, read from this host's contact records.
        services.AddScoped<ISmsContactTimeZoneResolver, SmsContactTimeZoneResolver>();

        // Resolve the contact for a number so conversations link to it. The contact types are read from the
        // content definitions, so contact search works without the management administration.
        services.AddScoped<ISmsContactResolver, SmsContactResolver>();
        services.TryAddScoped<IOmnichannelContactTypeProvider, ContentDefinitionOmnichannelContactTypeProvider>();

        // Real-time messaging notifications over the portal's hub.
        services.AddScoped<ISmsRealTimeNotifier, SmsRealTimeNotifier>();

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
