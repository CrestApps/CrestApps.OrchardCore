using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Tools;
using CrestApps.OrchardCore.OpenAI.Tools.Functions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }

    public static IServiceCollection AddOpenAIChatTool<TTool>(this IServiceCollection services)
        where TTool : class, IOpenAIChatTool
    {
        services
            .AddScoped<TTool>()
            .AddScoped<IOpenAIChatTool>(sp => sp.GetService<TTool>());

        return services;
    }

    public static IServiceCollection AddOpenAIChatTool<TTool, TFunction>(this IServiceCollection services)
        where TTool : class, IOpenAIChatTool
        where TFunction : class, IOpenAIChatFunction
    {
        services
            .AddOpenAIChatFunction<TFunction>()
            .AddScoped<TTool>()
            .AddScoped<IOpenAIChatTool>(sp => sp.GetService<TTool>());

        return services;
    }

    public static IServiceCollection AddOpenAIChatFunction<TFunction>(this IServiceCollection services)
        where TFunction : class, IOpenAIChatFunction
    {
        services
            .AddScoped<TFunction>()
            .AddScoped<IOpenAIChatFunction>(sp => sp.GetService<TFunction>());

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

        services
            .AddScoped<TService>()
            .AddScoped<IOpenAIChatCompletionService>(sp => sp.GetService<TService>())
            .AddKeyedScoped<IOpenAIChatCompletionService>(sourceKey, (sp, key) => sp.GetService<TService>());

        return services;
    }
}
