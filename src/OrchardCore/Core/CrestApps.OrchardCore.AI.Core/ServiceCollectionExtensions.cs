using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;

namespace CrestApps.OrchardCore.AI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAICoreServices(this IServiceCollection services)
    {
        services
            .AddCrestAppsAI()
            .AddCatalogs()
            .AddCatalogManagers()
            .AddScoped<INamedCatalog<AIProfile>, DefaultAIProfileStore>()
            .AddScoped<AIProviderConnectionStore>()
            .AddScoped<ICatalog<AIProviderConnection>>(sp => sp.GetRequiredService<AIProviderConnectionStore>())
            .AddScoped<INamedCatalog<AIProviderConnection>>(sp => sp.GetRequiredService<AIProviderConnectionStore>())
            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<ICatalogEntryHandler<AIProfile>, AIProfileHandler>();

        services
            .AddScoped<IAuthorizationHandler, AIProfileAuthorizationHandler>()
            .AddScoped<IAuthorizationHandler, AIToolAuthorizationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.CollectionName));

        return services;
    }

    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<DefaultAIDeploymentStore>()
            .AddScoped<ICatalog<AIDeployment>>(sp => sp.GetRequiredService<DefaultAIDeploymentStore>())
            .AddScoped<INamedCatalog<AIDeployment>>(sp => sp.GetRequiredService<DefaultAIDeploymentStore>())
            .AddScoped<INamedSourceCatalog<AIDeployment>>(sp => sp.GetRequiredService<DefaultAIDeploymentStore>())
            .AddScoped<IAIDeploymentManager, DefaultAIDeploymentManager>()
            .AddScoped<ICatalogEntryHandler<AIDeployment>, AIDeploymentHandler>();

        return services;
    }

    public static IServiceCollection AddAIDataSourceServices(this IServiceCollection services)
    {
        services
            .AddScoped<ICatalog<AIDataSource>, DefaultAIDataSourceStore>()
            .AddScoped<ICatalogManager<AIDataSource>, DefaultAIDataSourceManager>()
            .AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceHandler>();

        return services;
    }
}
