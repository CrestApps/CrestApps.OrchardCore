using CrestApps.OrchardCore.DeepSeek.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.DeepSeek.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeepSeekChatServices(this IServiceCollection services)
    {
        services
            .AddTransient<IConfigureOptions<DefaultDeepSeekOptions>, DefaultDeepSeekOptionsConfiguration>();

        return services;
    }
}
