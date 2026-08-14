using CrestApps.OrchardCore.Dialpad.Drivers;
using CrestApps.OrchardCore.Dialpad.Services;
using Microsoft.Extensions.DependencyInjection;
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

                options.CircuitBreaker.FailureRatio = 0.1;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 100;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);
            });

        services.AddTelephonyProviderOptionsConfiguration<DialpadProviderOptionsConfigurations>();
        services.AddSiteDisplayDriver<DialpadSettingsDisplayDriver>();
    }
}
