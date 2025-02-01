using CrestApps.OrchardCore.OpenAI.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAIChatServices(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<DefaultOpenAIOptions>, DefaultOpenAIOptionsConfiguration>();

        return services;
    }
}
