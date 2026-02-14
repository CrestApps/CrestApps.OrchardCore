using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core.Extensions;

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

    public static bool TryGetFirst(this AIFunctionArguments arguments, string key, out object value)
    {
        return arguments.TryGetValue(key, out value) && value is not null;
    }

    public static T GetFirstValueOrDefault<T>(this AIFunctionArguments arguments, string key, T fallbackValue = default)
    {
        if (arguments.TryGetFirst<T>(key, out var value))
        {
            return value;
        }

        return fallbackValue;
    }

    public static bool TryGetFirstString(this AIFunctionArguments arguments, string key, out string value)
        => arguments.TryGetFirstString(key, false, out value);

    public static bool TryGetFirstString(this AIFunctionArguments arguments, string key, bool allowEmptyString, out string value)
    {
        if (arguments.TryGetFirst(key, out value))
        {
            if (!allowEmptyString && string.IsNullOrEmpty(value))
            {
                value = null;

                return false;
            }

            return true;
        }

        value = null;

        return false;
    }

    public static bool TryGetFirst<T>(this AIFunctionArguments arguments, string key, out T value)
    {
        value = default;

        if (!arguments.TryGetValue(key, out var unsafeValue) || unsafeValue is null)
        {
            return false;
        }

        try
        {
            if (unsafeValue is T alreadyTyped)
            {
                value = alreadyTyped;

                return true;
            }

            if (unsafeValue is JsonElement je)
            {
                value = JsonSerializer.Deserialize<T>(je.GetRawText(), JSOptions.CaseInsensitive);

                return true;
            }

            // Handle nullable types (e.g. int?, DateTime?).
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            var safeValue = Convert.ChangeType(unsafeValue, targetType);

            value = (T)safeValue;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
