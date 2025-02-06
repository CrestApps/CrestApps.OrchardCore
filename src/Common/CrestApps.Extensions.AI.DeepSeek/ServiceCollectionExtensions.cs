using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CrestApps.Extensions.AI.DeepSeek;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeepSeekChatServices(this IServiceCollection services)
    {
        services
            .AddHttpClient("DeepSeek")
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
