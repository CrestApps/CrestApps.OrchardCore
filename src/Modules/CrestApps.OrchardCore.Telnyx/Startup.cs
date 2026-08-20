using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Extensions;
using CrestApps.OrchardCore.Telnyx.Drivers;
using CrestApps.OrchardCore.Telnyx.Endpoints;
using CrestApps.OrchardCore.Telnyx.Indexes;
using CrestApps.OrchardCore.Telnyx.Migrations;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
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

        services
            .AddOptions<TelnyxOptions>()
            .Services
            .AddTransient<IConfigureOptions<TelnyxOptions>, TelnyxOptionsConfigurations>()
            .AddTelephonyProviderOptionsConfiguration<TelnyxProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<TelnyxSettingsDisplayDriver>();

        services
            .AddScoped<ITelnyxWebhookService, TelnyxWebhookService>()
            .AddScoped<ITelnyxInboundCallRouter, TelnyxDirectInboundCallRouter>()
            .AddScoped<ITelnyxOutboundBridgeOrchestrator, TelnyxOutboundBridgeOrchestrator>()
            .AddScoped<ITelnyxAgentCredentialStore, TelnyxAgentCredentialStore>()
            .AddScoped<ITelnyxTelephonyCredentialIssuer, TelnyxTelephonyCredentialIssuer>()
            .AddScoped<ITelnyxProvisioningApiService, TelnyxProvisioningApiService>()
            .AddScoped<ISoftPhoneRegistrationConfigContributor, TelnyxSoftPhoneRegistrationConfigContributor>()
            .AddScoped<ISoftPhoneCredentialRevoker, TelnyxSoftPhoneCredentialRevoker>();

        services.AddIndexProvider<TelnyxAgentCredentialIndexProvider>();
        services.AddDataMigration<TelnyxAgentCredentialMigrations>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddTelnyxWebhookEndpoint();
    }
}

/// <summary>
/// Registers the Telnyx implementation of the Contact Center voice provider boundary.
/// </summary>
[Feature(TelnyxConstants.Feature.ContactCenterVoice)]
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
            .AddScoped<IContactCenterFeatureLifecycleParticipant, TelnyxContactCenterFeatureLifecycleParticipant>();
    }
}
