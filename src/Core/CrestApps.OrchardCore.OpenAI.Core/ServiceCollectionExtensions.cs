using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddModelDeploymentServices(this IServiceCollection services)
    {
        services.AddScoped<IModelDeploymentStore, DefaultModelDeploymentStore>();
        services.AddScoped<IModelDeploymentManager, DefaultModelDeploymentManager>();
        services.AddScoped<IModelDeploymentHandler, ModelDeploymentHandler>();
        services.AddPermissionProvider<OpenAIDeploymentProvider>();
        services.AddScoped<IAIChatSessionManager, DefaultAIChatSessionManager>();

        return services;
    }

    public static IServiceCollection AddAIChatProfileServices(this IServiceCollection services)
    {
        services.AddScoped<IAIChatProfileStore, DefaultAIChatProfileStore>();
        services.AddScoped<IAIChatProfileManager, DefaultAIChatProfileManager>();
        services.AddScoped<IAIChatProfileHandler, AIChatProfileHandler>();

        services.AddPermissionProvider<AIChatPermissionsProvider>();
        services.AddScoped<IAuthorizationHandler, AIChatProfileAuthenticationHandler>();

        return services;
    }

    public static IServiceCollection AddAIChatProfileSource<TSource>(this IServiceCollection services, string sourceKey)
         where TSource : class, IAIChatProfileSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.AddScoped<TSource>();
        services.AddScoped<IAIChatProfileSource>(sp => sp.GetService<TSource>());
        services.AddKeyedScoped<IAIChatProfileSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }

    public static IServiceCollection AddModelDeploymentSource<TSource>(this IServiceCollection services, string sourceKey)
        where TSource : class, IModelDeploymentSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.AddScoped<TSource>();
        services.AddScoped<IModelDeploymentSource>(sp => sp.GetService<TSource>());
        services.AddKeyedScoped<IModelDeploymentSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }

    public static IServiceCollection AddChatCompletionService<TService>(this IServiceCollection services, string sourceKey)
        where TService : class, IChatCompletionService
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.AddScoped<TService>();
        services.AddScoped<IChatCompletionService>(sp => sp.GetService<TService>());
        services.AddKeyedScoped<IChatCompletionService>(sourceKey, (sp, key) => sp.GetService<TService>());

        return services;
    }
}
