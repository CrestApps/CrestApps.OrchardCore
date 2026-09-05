using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Extensions;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telnyx.BackgroundTasks;
using CrestApps.OrchardCore.Telnyx.Drivers;
using CrestApps.OrchardCore.Telnyx.Endpoints;
using CrestApps.OrchardCore.Telnyx.Indexes;
using CrestApps.OrchardCore.Telnyx.Migrations;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
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
        services.AddHttpClient(TelnyxConstants.ProviderTechnicalName)
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Never auto-replay non-idempotent requests. This client carries call-origination POSTs and
                // credential mutations; a retried POST after a lost response could place a second outbound
                // call or mint a second credential. Outbound dials carry a Telnyx command_id for idempotency,
                // but the transport must not replay unsafe methods on its own. Safe methods (status GETs) still
                // retry.
                options.Retry.DisableForUnsafeHttpMethods();

                options.CircuitBreaker.FailureRatio = 0.1;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 100;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });

        // The typed client every Telnyx call now goes through. It is registered against the same named client so
        // it inherits the resilience handler above; its own retry policy sits inside it and decides per command
        // whether repeating is safe, which the transport cannot know.
        services.AddHttpClient<TelnyxApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TelnyxOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
            {
                client.BaseAddress = new Uri(options.ApiBaseUrl);
            }
        });

        services.AddSingleton<TelnyxApiRetryPolicy>();

        services
            .AddOptions<TelnyxOptions>()
            .Services
            .AddTransient<IConfigureOptions<TelnyxOptions>, TelnyxOptionsConfigurations>()
            .AddSignalOptionsChangeTokenSource<TelnyxOptions>()
            .AddTelephonyProviderOptionsConfiguration<TelnyxProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<TelnyxSettingsDisplayDriver>();

        services
            .AddScoped<ITelnyxWebhookService, TelnyxWebhookService>()
            .AddScoped<ITelnyxVoicemailRecordingStarter, TelnyxVoicemailRecordingStarter>()
            .AddScoped<IVoiceMediaProvisioner, TelnyxVoiceMediaProvisioner>()
            .AddScoped<ITelnyxOutboundBridgeOrchestrator, TelnyxOutboundBridgeOrchestrator>()
            .AddScoped<ITelnyxAgentCredentialStore, TelnyxAgentCredentialStore>()
            // Every path that dials an agent resolves the endpoint here, so none of them can pick a credential
            // the browser is not registered on.
            .AddScoped<ITelnyxAgentEndpointResolver, TelnyxAgentEndpointResolver>()
            .AddScoped<ITelnyxTelephonyCredentialIssuer, TelnyxTelephonyCredentialIssuer>()
            .AddScoped<ITelnyxProvisioningApiService, TelnyxProvisioningApiService>()
            .AddScoped<ISoftPhoneRegistrationConfigContributor, TelnyxSoftPhoneRegistrationConfigContributor>()
            .AddScoped<ISoftPhoneCredentialRevoker, TelnyxSoftPhoneCredentialRevoker>()
            .AddScoped<ISoftPhoneCredentialRegistrar, TelnyxSoftPhoneCredentialRegistrar>()
            // The automated-voice media seam. Registered here rather than with the Contact Center voice
            // provider because an automated conversation does not need a contact center to run.
            .AddScoped<IVoiceAgentMediaProvider, TelnyxVoiceAgentMediaProvider>();

        // The base router never routes; the Contact Center Voice feature (DialerStartup) registers
        // ContactCenterTelnyxInboundCallRouter to take over inbound routing. TryAdd registers this no-op
        // fallback only when nothing else has, so the Contact Center router always wins whenever Voice is
        // enabled - regardless of the order the module's startup classes run in. A plain AddScoped here made
        // resolution depend on that ordering, so adding an unrelated startup class could (and did) let this
        // no-op router shadow the real one and silently drop every inbound Contact Center call.
        services.TryAddScoped<ITelnyxInboundCallRouter, TelnyxDirectInboundCallRouter>();

        // Same reasoning as the router above: the webhook service is part of the base feature and is built on
        // tenants that have never enabled Contact Center, so the digits sink needs a no-op fallback. Contact
        // Center's own registration takes over whenever it is enabled, whichever startup runs first.
        services.TryAddScoped<IInboundVoiceDigitsSink, NoInboundVoiceDigitsSink>();

        services.AddIndexProvider<TelnyxAgentCredentialIndexProvider>();
        services.AddDataMigration<TelnyxAgentCredentialMigrations>();

        // Soft-phone health metrics (companion tooling): a shell-lifetime recorder of credential-issuance and
        // webhook-processing outcomes, plus a passive canary that logs the snapshot and warns on a low
        // registration success rate.
        // Orphaned-call reconciliation: the only path that can see a call placed immediately before a restart,
        // for which no interaction was ever written and which no local sweep can therefore reach.
        services.AddScoped<TelnyxOrphanedCallReconciler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, TelnyxOrphanedCallReconciliationBackgroundTask>());

        services.AddSingleton<ISoftPhoneHealthMetrics, SoftPhoneHealthMetrics>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SoftPhoneHealthCanaryBackgroundTask>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddTelnyxWebhookEndpoint();
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
                    TelnyxConstants.Feature.Area,
                    TelnyxConstants.ContactCenterVoiceWorkPartition,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new TelnyxContactCenterFeatureLifecycleParticipant(
                    ContactCenterConstants.Feature.Voice,
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
                    TelnyxConstants.Feature.Area,
                    TelnyxConstants.ContactCenterMediaWorkPartition,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()))
            .AddScoped<IContactCenterFeatureLifecycleParticipant>(serviceProvider =>
                new TelnyxContactCenterFeatureLifecycleParticipant(
                    ContactCenterConstants.Feature.VoiceMedia,
                    TelnyxConstants.ContactCenterMediaWorkPartition,
                    serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>(),
                    serviceProvider.GetRequiredService<IOptions<ContactCenterFeatureLifecycleOptions>>()));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // Telnyx dials the media-stream endpoint as a raw WebSocket. The WebSocket middleware itself is added by the
        // CrestApps.OrchardCore.WebSockets feature, which the Telnyx module depends on, so this only maps the route.
        routes.AddTelnyxMediaStreamEndpoint();
    }
}
