using CrestApps.OrchardCore.Asterisk.Drivers;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using Polly;

namespace CrestApps.OrchardCore.Asterisk;

/// <summary>
/// Registers the Asterisk telephony providers and their settings driver.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(AsteriskConstants.HttpClientName)
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                options.CircuitBreaker.FailureRatio = 0.1;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 100;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });

        services
            .AddTelephonyProviderOptionsConfiguration<AsteriskProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<AsteriskSettingsDisplayDriver>()
            .AddTransient<IConfigureOptions<DefaultAsteriskOptions>, DefaultAsteriskOptionsConfiguration>();

        services
            .AddSingleton<AsteriskRealtimeVoiceListener>()
            .AddScoped<AsteriskRealtimeVoiceEventDispatcher>()
            .AddScoped<IModularTenantEvents, AsteriskRealtimeVoiceTenantEvents>();
    }
}
