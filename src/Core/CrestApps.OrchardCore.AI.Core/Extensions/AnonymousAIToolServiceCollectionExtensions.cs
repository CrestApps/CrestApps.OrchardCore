using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core.Extensions;

/// <summary>
/// Marks AI tools safe for visitors who are not signed in.
/// </summary>
public static class AnonymousAIToolServiceCollectionExtensions
{
    /// <summary>
    /// Marks a registered AI tool safe for anonymous callers, such as a storefront visitor's in-browser agent. Mark a
    /// tool only when anyone may run it: it reads public data or acts on the caller's own data, never reveals another
    /// person's data, and never pays, deletes or changes the site.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="toolName">The name the tool is registered under with <c>AddCoreAITool</c>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection MarkAIToolAnonymousSafe(this IServiceCollection services, string toolName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        services.Configure<AnonymousAIToolOptions>(options => options.MarkSafe(toolName));

        return services;
    }
}
