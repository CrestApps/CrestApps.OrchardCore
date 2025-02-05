using CrestApps.OrchardCore.DeepSeek.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.DeepSeek.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeepSeekChatServices(this IServiceCollection services)
    {
        services
            .AddTransient<IConfigureOptions<DefaultDeepSeekOptions>, DefaultDeepSeekOptionsConfiguration>()
            .AddHttpClient(DeepSeekConstants.DeepSeekProviderName)
            .AddStandardResilienceHandler(options =>
            {
                options.Retry = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                };
            });

        return services;
    }
}
