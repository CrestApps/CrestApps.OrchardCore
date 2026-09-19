using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Telephony.Extensions;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.BackgroundTasks;
using CrestApps.OrchardCore.Telnyx.Core;
using CrestApps.OrchardCore.Telnyx.Core.Services;
using CrestApps.OrchardCore.Telnyx.Drivers;
using CrestApps.OrchardCore.Telnyx.Endpoints;
using CrestApps.OrchardCore.Telnyx.Indexes;
using CrestApps.OrchardCore.Telnyx.Migrations;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Data;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using Polly;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the Telnyx telephony provider, its browser WebRTC credential issuance, its settings driver,
/// and its signed call-event webhook endpoint.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContactCenterCapability(TelnyxConstants.Feature.Area, TelnyxConstants.ProviderTechnicalName);

        services.AddCoreHostSeams();

        services.AddCoreTelnyx();

        services
            .AddOptions<TelnyxOptions>()
            .Services
            .AddTransient<IConfigureOptions<TelnyxOptions>, TelnyxOptionsConfigurations>()
            .AddSignalOptionsChangeTokenSource<TelnyxOptions>()
            .AddTelephonyProviderOptionsConfiguration<TelnyxProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<TelnyxSettingsDisplayDriver>();

        services.AddIndexProvider<TelnyxAgentCredentialIndexProvider>();
        services.AddDataMigration<TelnyxAgentCredentialMigrations>();

        // The host's scheduler for the cycles AddCoreTelnyx registered.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, TelnyxOrphanedCallReconciliationBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SoftPhoneHealthCanaryBackgroundTask>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapTelnyxWebhookEndpoints();
    }
}

/// <summary>
/// Registers the Telnyx implementation of the Contact Center voice provider boundary. This is integration
/// glue rather than a separately selectable feature: it activates automatically whenever the Telnyx provider
/// and Contact Center Voice are both enabled, so an operator never has to enable a redundant per-provider
/// toggle that must match the provider they already configured.
/// </summary>
[RequireFeatures(ContactCenterConstants.Feature.Voice)]
public sealed class DialerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<TelnyxContactCenterVoiceProvider>()
            .AddScoped<IContactCenterVoiceProvider>(sp => sp.GetRequiredService<TelnyxContactCenterVoiceProvider>())
            .AddSingleton<IProviderIdentityProvider, TelnyxProviderIdentityProvider>()
            .AddScoped<ITelnyxInboundCallRouter, ContactCenterTelnyxInboundCallRouter>()
            .AddScoped<IProviderWebhookInboxHandler, TelnyxWebhookInboxHandler>()
            // What a waiting caller hears. Replaces the no-op the Queues feature registers, so a tenant that
            // configures queue treatment on Telnyx actually gets it rather than silence.
            .AddScoped<IQueueTreatmentProvider, TelnyxQueueTreatmentProvider>()
            .AddScoped<IIvrProvider, TelnyxIvrProvider>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new TelnyxContactCenterFeatureLifecycleParticipant(
                    TelnyxConstants.ProviderTechnicalName,
                    TelnyxConstants.ContactCenterVoiceWorkPartition,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new TelnyxContactCenterFeatureLifecycleParticipant(
                    ContactCenterCapabilities.Voice,
                    TelnyxConstants.ContactCenterVoiceWorkPartition,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));

        // Secure recording ingestion: the saved-recording webhook enqueues a durable job, and the background
        // sweep downloads each recording into the encrypted media store with retry and dead-lettering.
        services
            .AddScoped<ITelnyxRecordingIngestJobStore, TelnyxRecordingIngestJobStore>()
            .AddScoped<ITelnyxRecordingIngestService, TelnyxRecordingIngestService>()
            .AddScoped<ITelnyxRecordingSavedHandler, TelnyxRecordingIngestEnqueuer>();

        services.AddIndexProvider<TelnyxRecordingIngestJobIndexProvider>();
        services.AddDataMigration<TelnyxRecordingIngestJobMigrations>();
        services.AddBackgroundCycle<ITelnyxRecordingIngestCycle, TelnyxRecordingIngestCycle>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, TelnyxRecordingIngestBackgroundTask>());
    }
}

/// <summary>
/// Registers the Telnyx implementation of the Contact Center bidirectional voice-media boundary through Telnyx Media
/// Streaming. Like the voice adapter this is integration glue rather than a separately selectable feature: it
/// activates automatically whenever the Telnyx provider and Contact Center Voice Media are both enabled. It maps the
/// WebSocket endpoint Telnyx dials back to; the WebSocket middleware itself comes from the WebSockets feature the
/// module depends on.
/// </summary>
[RequireFeatures(ContactCenterConstants.Feature.VoiceMedia)]
public sealed class TelnyxContactCenterMediaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterVoiceMediaProvider, TelnyxContactCenterVoiceMediaProvider>()
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new TelnyxContactCenterFeatureLifecycleParticipant(
                    TelnyxConstants.ProviderTechnicalName,
                    TelnyxConstants.ContactCenterMediaWorkPartition,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new TelnyxContactCenterFeatureLifecycleParticipant(
                    ContactCenterCapabilities.VoiceMedia,
                    TelnyxConstants.ContactCenterMediaWorkPartition,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // Telnyx dials the media-stream endpoint as a raw WebSocket. The WebSocket middleware itself is added by the
        // CrestApps.OrchardCore.WebSockets feature, which the Telnyx module depends on, so this only maps the route.
        routes.MapTelnyxMediaStreamEndpoint();
    }
}
