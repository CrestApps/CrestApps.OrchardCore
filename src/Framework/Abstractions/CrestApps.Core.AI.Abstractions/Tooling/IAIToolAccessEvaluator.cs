using System.Security.Claims;

namespace CrestApps.Core.AI.Tooling;

/// <summary>
/// Evaluates whether a given user is authorized to use a specific AI tool.
/// </summary>
public interface IAIToolAccessEvaluator
{
    /// <summary>
    /// Determines whether the specified user is allowed to invoke the given tool.
    /// </summary>
    /// <param name="user">The current user principal. May be <c>null</c> for anonymous requests.</param>
    /// <param name="toolName">The name of the AI tool to authorize.</param>
    /// <returns><c>true</c> if the user is authorized; otherwise <c>false</c>.</returns>
    Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName);
}
