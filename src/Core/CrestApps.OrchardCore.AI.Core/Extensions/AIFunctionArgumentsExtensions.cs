using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.Extensions;

/// <summary>
/// Provides extension methods for AI function arguments.
/// </summary>
public static class AIFunctionArgumentsExtensions
{
    /// <summary>
    /// Checks if the current request is authorized for the given permission,
    /// automatically bypassing authorization for unauthenticated MCP requests
    /// since the MCP server handles authentication via its own policy.
    /// </summary>
    public static Task<bool> IsAuthorizedAsync(this AIFunctionArguments arguments, Permission permission)
    {
        var user = GetUser(arguments);

        if (IsMcpRequestWithUnauthenticatedUser(arguments, user))
        {
            return Task.FromResult(true);
        }

        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();

        return authorizationService.AuthorizeAsync(user, permission);
    }

    /// <summary>
    /// Checks if the current request is authorized for the given permission and resource,
    /// automatically bypassing authorization for unauthenticated MCP requests
    /// since the MCP server handles authentication via its own policy.
    /// </summary>
    public static Task<bool> IsAuthorizedAsync(this AIFunctionArguments arguments, Permission permission, object resource)
    {
        var user = GetUser(arguments);

        if (IsMcpRequestWithUnauthenticatedUser(arguments, user))
        {
            return Task.FromResult(true);
        }

        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();

        return authorizationService.AuthorizeAsync(user, permission, resource);
    }

    /// <summary>
    /// Returns <see langword="true"/> if the current user is authenticated, or if the request
    /// originates from the MCP server with no authenticated user (since MCP handles
    /// authentication via its own policy).
    /// </summary>
    public static bool IsAuthenticatedOrMcpRequest(this AIFunctionArguments arguments)
    {
        var user = GetUser(arguments);

        if (user?.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        // Allow unauthenticated MCP requests since MCP server handles auth via policy.

        return arguments.Context?.ContainsKey("mcpRequest") == true;
    }

    private static ClaimsPrincipal GetUser(AIFunctionArguments arguments)
    {
        return arguments.Services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
    }

    private static bool IsMcpRequestWithUnauthenticatedUser(AIFunctionArguments arguments, ClaimsPrincipal user)
    {
        return arguments.Context?.ContainsKey("mcpRequest") == true
            && user?.Identity?.IsAuthenticated != true;
    }
}
