using System.Security.Claims;
using CrestApps.Core.AI.Tooling;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Orchard Core implementation of <see cref="IAIToolAccessEvaluator"/> that
/// delegates to the Orchard Core permission system.
/// </summary>
public sealed class OrchardCoreAIToolAccessEvaluator : IAIToolAccessEvaluator
{
    private readonly IAuthorizationService _authorizationService;

    public OrchardCoreAIToolAccessEvaluator(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName)
    {
        return await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, toolName as object);
    }
}
