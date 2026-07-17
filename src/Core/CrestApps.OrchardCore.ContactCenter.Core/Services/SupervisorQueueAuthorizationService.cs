using System;
using System.Linq;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Default supervisor queue authorization service backed by monitor permission and queue entitlements.
/// </summary>
public sealed class SupervisorQueueAuthorizationService : ISupervisorQueueAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisorQueueAuthorizationService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="agentManager">The agent profile manager used to resolve supervisor entitlements.</param>
    public SupervisorQueueAuthorizationService(
        IAuthorizationService authorizationService,
        IAgentProfileManager agentManager)
    {
        _authorizationService = authorizationService;
        _agentManager = agentManager;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAuthorizedAsync(
        ClaimsPrincipal principal,
        string userId,
        string queueId,
        CancellationToken cancellationToken = default)
    {
        if (principal is null ||
            string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(queueId))
        {
            return false;
        }

        if (!await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.MonitorContactCenter))
        {
            return false;
        }

        var supervisor = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        return supervisor?.AllowedQueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true;
    }
}
