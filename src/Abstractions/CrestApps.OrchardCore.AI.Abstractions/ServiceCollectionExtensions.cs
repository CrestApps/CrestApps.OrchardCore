using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAITool<TTool>(this IServiceCollection services, string name, Action<AIToolDefinitionEntry> configure = null)
        where TTool : AITool
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services
            .AddCoreAITool<TTool>(name)
            .Configure<AIToolDefinitionOptions>(o =>
            {
                o.Add<TTool>(name, (t) =>
                {

                    configure(t);

                    t.Name = name;
                });
            });

        return services;
    }


    public static IServiceCollection AddCoreAITool<TTool>(this IServiceCollection services, string name)
        where TTool : AITool
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.AddSingleton<TTool>();
        services.AddKeyedSingleton<AITool>(name, (sp, key) => sp.GetRequiredService<TTool>());

        return services;
    }

    public static IServiceCollection AddAIToolSource<TSource>(this IServiceCollection services, string source)
        where TSource : class, IAIToolSource
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        services.AddScoped<TSource>();
        services.AddScoped<IAIToolSource>(sp => sp.GetRequiredService<TSource>());
        services.AddKeyedScoped<IAIToolSource>(source, (sp, key) => sp.GetRequiredService<TSource>());

        return services;
    }
}
