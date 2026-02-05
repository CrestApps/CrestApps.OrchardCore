using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an MCP resource type with its handler.
    /// </summary>
    /// <typeparam name="THandler">The type of handler for this resource type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="type">The unique type identifier for this resource type.</param>
    /// <param name="configure">Optional configuration action for the resource type entry.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpResourceType<THandler>(
        this IServiceCollection services,
        string type,
        Action<McpResourceTypeEntry> configure = null)
        where THandler : class, IMcpResourceTypeHandler
    {
        services.Configure<McpResourceOptions>(options =>
        {
            options.AddResourceType(type, configure);
        });

        services.AddScoped<IMcpResourceTypeHandler, THandler>();

        return services;
    }
}
