using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Functions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAIDeploymentServices(this IServiceCollection services)
    {
        services.AddScoped<IOpenAIDeploymentStore, DefaultOpenAIDeploymentStore>();
        services.AddScoped<IOpenAIDeploymentManager, DefaultOpenAIDeploymentManager>();
        services.AddScoped<IOpenAIDeploymentHandler, OpenAIDeploymentHandler>();
        services.AddPermissionProvider<OpenAIDeploymentProvider>();

        return services;
    }

    public static IServiceCollection AddOpenAIChatProfileServices(this IServiceCollection services)
    {
        services.AddScoped<IOpenAIChatProfileStore, DefaultOpenAIChatProfileStore>();
        services.AddScoped<IOpenAIChatProfileManager, DefaultOpenAIChatProfileManager>();
        services.AddScoped<IOpenAIChatProfileHandler, OpenAIChatProfileHandler>();
        services.AddScoped<IOpenAIChatSessionManager, DefaultOpenAIChatSessionManager>();

        services.AddPermissionProvider<OpenAIChatPermissionsProvider>();
        services.AddScoped<IAuthorizationHandler, OpenAIChatProfileAuthenticationHandler>();
        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(OpenAIConstants.CollectionName));

        return services;
    }

    public static IServiceCollection AddOpenAIChatFunction<TFunction>(this IServiceCollection services, string functionName)
        where TFunction : class, IOpenAIChatFunction
    {
        ArgumentNullException.ThrowIfNull(functionName);

        services.AddScoped<TFunction>();
        services.AddScoped<IOpenAIChatFunction>(sp => sp.GetService<TFunction>());
        services.AddKeyedScoped<IOpenAIChatFunction>(functionName, (sp, key) => sp.GetService<TFunction>());

        return services;
    }

    public static IServiceCollection AddOpenAIChatProfileSource<TSource>(this IServiceCollection services, string sourceKey)
         where TSource : class, IOpenAIChatProfileSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.AddScoped<TSource>();
        services.AddScoped<IOpenAIChatProfileSource>(sp => sp.GetService<TSource>());
        services.AddKeyedScoped<IOpenAIChatProfileSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }

    public static IServiceCollection AddOpenAIDeploymentSource<TSource>(this IServiceCollection services, string sourceKey)
        where TSource : class, IOpenAIDeploymentSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.AddScoped<TSource>();
        services.AddScoped<IOpenAIDeploymentSource>(sp => sp.GetService<TSource>());
        services.AddKeyedScoped<IOpenAIDeploymentSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }

    public static IServiceCollection AddOpenAIChatCompletionService<TService>(this IServiceCollection services, string sourceKey)
        where TService : class, IOpenAIChatCompletionService
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.AddScoped<TService>();
        services.AddScoped<IOpenAIChatCompletionService>(sp => sp.GetService<TService>());
        services.AddKeyedScoped<IOpenAIChatCompletionService>(sourceKey, (sp, key) => sp.GetService<TService>());

        return services;
    }
}
