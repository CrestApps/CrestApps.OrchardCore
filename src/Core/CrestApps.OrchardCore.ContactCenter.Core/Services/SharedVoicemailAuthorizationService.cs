using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="ISharedVoicemailAuthorizationService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two things must both hold for a user to see a queue's shared voicemail: they hold
/// <see cref="ContactCenterPermissions.AccessSharedVoicemail"/>, and they are entitled to that queue. The permission
/// says the user's role answers shared voicemail at all; the entitlement says which teams they belong to, so a user
/// allowed to answer voicemail still never hears another team's callers.
/// </para>
/// <para>
/// A queue is the user's when their agent profile is granted it and the tenant's entitlement policy still allows it
/// (an agent), or when the supervisor queue authorization lets them oversee it (a supervisor). A user who manages the
/// whole Contact Center configures every queue and every entitlement anyway, so they see every box.
/// </para>
/// </remarks>
public sealed class SharedVoicemailAuthorizationService : ISharedVoicemailAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailAuthorizationService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used for the permission checks.</param>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the user's queue entitlements.</param>
    /// <param name="entitlementPolicy">The policy that decides which queues an agent may serve.</param>
    /// <param name="supervisorQueueAuthorizationService">The authorization that decides which queues a supervisor oversees.</param>
    public SharedVoicemailAuthorizationService(
        IAuthorizationService authorizationService,
        IAgentProfileManager agentProfileManager,
        IAgentEntitlementPolicy entitlementPolicy,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService)
    {
        _authorizationService = authorizationService;
        _agentProfileManager = agentProfileManager;
        _entitlementPolicy = entitlementPolicy;
        _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
    }

    /// <inheritdoc/>
    public async Task<SharedVoicemailAccess> GetAccessAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) ||
            !await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.AccessSharedVoicemail))
        {
            return SharedVoicemailAccess.None;
        }

        var userName = principal.Identity?.Name;
        var canManage = await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.ManageSharedVoicemail);

        if (await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.ManageContactCenter))
        {
            return new SharedVoicemailAccess
            {
                UserId = userId,
                UserName = userName,
                CanAccess = true,
                AllQueues = true,
                CanManage = canManage,
            };
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(userId, cancellationToken);
        var queueIds = new List<string>();

        foreach (var queueId in AgentEntitlementUtilities.NormalizeIds(agent?.AllowedQueueIds))
        {
            if (_entitlementPolicy.AllowsQueue(agent, queueId) ||
                await _supervisorQueueAuthorizationService.IsAuthorizedAsync(principal, userId, queueId, cancellationToken))
            {
                queueIds.Add(queueId);
            }
        }

        return new SharedVoicemailAccess
        {
            UserId = userId,
            UserName = userName,
            CanAccess = true,
            QueueIds = queueIds,
            CanManage = canManage,
        };
    }
}
