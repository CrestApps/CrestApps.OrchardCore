using CrestApps.AI.A2A.Functions;
using CrestApps.AI.A2A.Handlers;
using CrestApps.AI.A2A.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.AI.A2A;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCrestAppsA2AClient(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddMemoryCache();
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAICompletionContextBuilderHandler, A2AAICompletionContextBuilderHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, A2AToolRegistryProvider>());
        services.TryAddSingleton<IA2AAgentCardCacheService, DefaultA2AAgentCardCacheService>();
        services.TryAddScoped<IA2AConnectionAuthService, DefaultA2AConnectionAuthService>();

        services.AddAITool<ListAvailableAgentsFunction>(ListAvailableAgentsFunction.TheName);
        services.AddAITool<FindAgentForTaskFunction>(FindAgentForTaskFunction.TheName);
        services.AddAITool<FindToolsForTaskFunction>(FindToolsForTaskFunction.TheName);

        return services;
    }
}
