using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.Extensions;

/// <summary>
/// Registers the permission an AI tool needs from the person driving the conversation.
/// </summary>
public static class AIToolPermissionServiceCollectionExtensions
{
    /// <summary>
    /// Sets the permission a registered AI tool needs from its caller. The tool checks it before it acts
    /// (<see cref="AIToolCallerAuthorization.IsAuthorizedForToolAsync"/>), treating a caller who isn't signed in as
    /// refused, and a profile that people without the permission can use doesn't save with the tool.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="toolName">The name the tool is registered under with <c>AddCoreAITool</c>.</param>
    /// <param name="permission">The permission the caller must hold.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection RequireAIToolPermission(this IServiceCollection services, string toolName, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(permission);

        services.Configure<AIToolPermissionOptions>(options => options.Require(toolName, permission));

        return services;
    }
}
