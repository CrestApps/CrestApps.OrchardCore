using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an AI tool with the builder pattern for fluent configuration.
    /// By default, tools are registered as system tools (hidden from UI).
    /// Call <see cref="AIToolBuilder{TTool}.Selectable"/> to make the tool visible for user selection.
    /// </summary>
    /// <typeparam name="TTool">The tool type implementing <see cref="AITool"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique name for this tool.</param>
    /// <returns>A builder for fluent configuration of the tool.</returns>
    public static AIToolBuilder<TTool> AddAITool<TTool>(this IServiceCollection services, string name)
        where TTool : AITool
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.AddCoreAITool<TTool>(name);

        var entry = new AIToolDefinitionEntry(typeof(TTool))
        {
            Name = name,
            IsSystemTool = true,
        };

        services.Configure<AIToolDefinitionOptions>(o =>
        {
            if (string.IsNullOrEmpty(entry.Title))
            {
                entry.Title = name;
            }

            if (string.IsNullOrEmpty(entry.Description))
            {
                entry.Description = name;
            }

            o.SetTool(name, entry);
        });

        return new AIToolBuilder<TTool>(entry);
    }

    /// <summary>
    /// Registers the core DI services for an AI tool (singleton and keyed singleton)
    /// without adding it to the tool definition options. Use this for tools that
    /// should only be resolved programmatically (e.g., MCP invoke function).
    /// </summary>
    public static IServiceCollection AddCoreAITool<TTool>(this IServiceCollection services, string name)
        where TTool : AITool
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.AddSingleton<TTool>();
        services.AddKeyedSingleton<AITool>(name, (sp, key) => sp.GetRequiredService<TTool>());

        return services;
    }
}
