using CrestApps.Core.ContactCenter;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.Hosting.Background;
using CrestApps.OrchardCore.Telephony;
using CrestApps.Core.Telephony.Services;
using CrestApps.OrchardCore.Telnyx.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using CrestApps.Core.Telephony;

namespace CrestApps.OrchardCore.Telnyx.Core;

/// <summary>
/// Registers the voice provider's services, independently of the host it runs in.
/// </summary>
public static class TelnyxServiceCollectionExtensions
{
    /// <summary>
    /// Adds the provider's transport, services and periodic work.
    /// </summary>
    /// <remarks>
    /// What stays with the host is everything the host alone can answer: how its settings are read and
    /// displayed, how its indexes and migrations are registered, and how its scheduler runs the cycles
    /// added here.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelnyx(this IServiceCollection services)
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
                // call or mint a second credential. Outbound dials carry a provider command id for
                // idempotency, but the transport must not replay unsafe methods on its own. Safe methods
                // (status GETs) still retry.
                options.Retry.DisableForUnsafeHttpMethods();

                options.CircuitBreaker.FailureRatio = 0.1;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 100;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });

        // The typed client every call now goes through. It is registered against the same named client so
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

        // The base router never routes; the Contact Center Voice feature registers a router that takes over
        // inbound routing. TryAdd registers this no-op fallback only when nothing else has, so the Contact
        // Center router always wins whenever Voice is enabled - regardless of the order registrations run in.
        // A plain AddScoped here made resolution depend on that ordering, so adding an unrelated startup class
        // could (and did) let this no-op router shadow the real one and silently drop every inbound Contact
        // Center call.
        services.TryAddScoped<ITelnyxInboundCallRouter, TelnyxDirectInboundCallRouter>();

        // Same reasoning as the router above: the webhook service is part of the base feature and is built on
        // tenants that have never enabled Contact Center, so the digits sink needs a no-op fallback. Contact
        // Center's own registration takes over whenever it is enabled, whichever runs first.
        services.TryAddScoped<IInboundVoiceDigitsSink, NoInboundVoiceDigitsSink>();

        // Orphaned-call reconciliation: the only path that can see a call placed immediately before a restart,
        // for which no interaction was ever written and which no local sweep can therefore reach.
        services.AddScoped<TelnyxOrphanedCallReconciler>();
        services.AddBackgroundCycle<ITelnyxOrphanedCallReconciliationCycle, TelnyxOrphanedCallReconciliationCycle>();

        // Soft-phone health metrics (companion tooling): a shell-lifetime recorder of credential-issuance and
        // webhook-processing outcomes, plus a passive canary that logs the snapshot and warns on a low
        // registration success rate.
        services.AddSingleton<ISoftPhoneHealthMetrics, SoftPhoneHealthMetrics>();
        services.AddBackgroundCycle<ISoftPhoneHealthCanaryCycle, SoftPhoneHealthCanaryCycle>();

        return services;
    }
}
