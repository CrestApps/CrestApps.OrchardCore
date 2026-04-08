using System.Security.Claims;
using CrestApps.Core.AI.Tooling;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Default implementation that permits all tool access.
/// Replace with an authorization-aware implementation for fine-grained control.
/// </summary>
internal sealed class DefaultAIToolAccessEvaluator : IAIToolAccessEvaluator
{
    public Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName)
        => Task.FromResult(true);
}
