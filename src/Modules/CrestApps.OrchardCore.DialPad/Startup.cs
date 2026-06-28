using CrestApps.OrchardCore.DialPad.Drivers;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using Polly;

namespace CrestApps.OrchardCore.DialPad;

/// <summary>
/// Registers the DialPad telephony provider and its settings driver.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(DialPadConstants.ProviderTechnicalName)
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

        services.AddTelephonyProviderOptionsConfiguration<DialPadProviderOptionsConfigurations>();
        services.AddSiteDisplayDriver<DialPadSettingsDisplayDriver>();
    }
}
