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

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreAIToolAccessEvaluator"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    public OrchardCoreAIToolAccessEvaluator(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Asynchronously performs the is authorized operation.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="toolName">The tool name.</param>
    public async Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName)
    {
        return await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, toolName as object);
    }
}
