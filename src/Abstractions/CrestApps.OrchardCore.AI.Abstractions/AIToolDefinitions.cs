using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAITool<TTool>(this IServiceCollection services, string name, Action<AIToolDefinitionEntry> configure = null)
        where TTool : AITool
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.AddScoped<TTool>();
        services.Configure<AIToolDefinitions>(o =>
        {
            o.Add<TTool>(name, configure);
        });

        return services;
    }

    public static IServiceCollection AddAIToolSource<TSource>(this IServiceCollection services, string source)
        where TSource : class, IAIToolSource
    {
        services.AddScoped<TSource>();
        services.AddScoped<IAIToolSource>(sp => sp.GetRequiredService<TSource>());
        services.AddKeyedScoped<IAIToolSource>(source, (sp, key) => sp.GetRequiredService<TSource>());

        return services;
    }
}

public class AIToolDefinitions
{
    private readonly Dictionary<string, AIToolDefinitionEntry> _tools = [];

    public IReadOnlyDictionary<string, AIToolDefinitionEntry> Tools => _tools;

    internal void Add<TTool>(string name, Action<AIToolDefinitionEntry> configure = null)
        where TTool : AITool
    {
        if (!_tools.TryGetValue(name, out var definition))
        {
            definition = new AIToolDefinitionEntry(typeof(TTool));
        }

        if (configure != null)
        {
            configure(definition);
        }

        _tools[name] = definition;
    }
}

public class AIToolDefinitionEntry
{
    public AIToolDefinitionEntry(Type type)
    {
        ToolType = type;
    }

    public Type ToolType { get; }

    public string Title { get; set; }

    public string Description { get; set; }
}
