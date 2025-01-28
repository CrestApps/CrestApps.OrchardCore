using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<IOpenAIDeploymentStore, DefaultOpenAIDeploymentStore>()
            .AddScoped<IOpenAIDeploymentManager, DefaultOpenAIDeploymentManager>()
            .AddScoped<IOpenAIDeploymentHandler, OpenAIDeploymentHandler>()
            .AddPermissionProvider<OpenAIDeploymentProvider>();

        return services;
    }

    public static IServiceCollection AddOpenAIChatProfileServices(this IServiceCollection services)
    {
        services
            .AddScoped<IOpenAIChatProfileStore, DefaultOpenAIChatProfileStore>()
            .AddScoped<IOpenAIChatProfileManager, DefaultOpenAIChatProfileManager>()
            .AddScoped<IOpenAIChatProfileManagerSession, DefaultOpenAIChatProfileManagerSession>()
            .AddScoped<IOpenAIChatProfileHandler, OpenAIChatProfileHandler>()
            .AddScoped<IOpenAIChatSessionManager, DefaultOpenAIChatSessionManager>();

        services
            .AddPermissionProvider<OpenAIChatPermissionsProvider>()
            .AddScoped<IAuthorizationHandler, OpenAIChatProfileAuthenticationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(OpenAIConstants.CollectionName));

        services.AddTransient<IConfigureOptions<DefaultOpenAIOptions>, DefaultOpenAIOptionsConfiguration>();

        return services;
    }

    public static IServiceCollection AddOpenAIChatProfileSource<TSource>(this IServiceCollection services, string sourceKey)
         where TSource : class, IOpenAIChatProfileSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services
            .AddScoped<TSource>()
            .AddScoped<IOpenAIChatProfileSource>(sp => sp.GetService<TSource>())
            .AddKeyedScoped<IOpenAIChatProfileSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }

    public static IServiceCollection AddOpenAIDeploymentSource<TSource>(this IServiceCollection services, string sourceKey)
        where TSource : class, IOpenAIDeploymentSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services
            .AddScoped<TSource>()
            .AddScoped<IOpenAIDeploymentSource>(sp => sp.GetService<TSource>())
            .AddKeyedScoped<IOpenAIDeploymentSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }

    public static IServiceCollection AddOpenAIChatCompletionService<TService>(this IServiceCollection services, string sourceKey)
        where TService : class, IOpenAIChatCompletionService
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.TryAddScoped<TService>();
        services.TryAddScoped<IOpenAIChatCompletionService>(sp => sp.GetService<TService>());
        services.AddKeyedScoped<IOpenAIChatCompletionService>(sourceKey, (sp, key) => sp.GetService<TService>());

        return services;
    }

    public static IServiceCollection AddOpenAITool<TTool>(this IServiceCollection services)
        where TTool : AITool
    {
        services.AddTransient<AITool, TTool>();

        return services;
    }
}
