using CrestApps.OrchardCore.Dialpad.Drivers;
using CrestApps.OrchardCore.Dialpad.Endpoints;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using Polly;

namespace CrestApps.OrchardCore.Dialpad;

/// <summary>
/// Registers the Dialpad telephony provider and its settings driver.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(DialpadConstants.ProviderTechnicalName)
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Never auto-replay non-idempotent requests. This client carries call-origination POSTs and
                // OAuth authorization-code/refresh-token POSTs; retrying a POST after a lost response could
                // place a second outbound call, and replaying a one-time authorization code or a rotated
                // refresh token yields invalid_grant after the first request already succeeded. Safe methods
                // (status GETs) still retry.
                options.Retry.DisableForUnsafeHttpMethods();

                options.CircuitBreaker.FailureRatio = 0.1;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 100;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });

        services
            .AddOptions<DialpadResolvedOptions>()
            .Services
            .AddTransient<IConfigureOptions<DialpadResolvedOptions>, DialpadResolvedOptionsConfigurations>()
            .AddTelephonyProviderOptionsConfiguration<DialpadProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<DialpadSettingsDisplayDriver>();
    }
}

/// <summary>
/// Registers Dialpad Contact Center voice integration endpoints.
/// </summary>
[Feature(DialpadConstants.Feature.ContactCenterVoice)]
public sealed class DialpadContactCenterStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddDialpadWebhookEndpoint();
    }
}
