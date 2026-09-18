using CrestApps.Core.Hosting;
using CrestApps.Core.Omnichannel.Sms.Portal.Security;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Hubs;

/// <summary>
/// The real-time SMS portal, independent of the host it runs in.
/// </summary>
/// <remarks>
/// <para>
/// On connect an agent joins their per-agent group and a group for every queue (department) they belong to, so
/// inbound-message and delivery notifications reach the right inboxes. Supervisors who may view all
/// conversations also join the unassigned (triage) group. The connection doubles as the agent's presence: the
/// portal checks in over it, and routed assignment only pushes conversations at agents whose portal has checked
/// in recently.
/// </para>
/// <para>
/// Everything host-specific is asked as a question rather than answered here: whether the caller may open the
/// portal, and which tenant the connection belongs to. A host binds those; this class names no web framework of
/// its own beyond SignalR.
/// </para>
/// </remarks>
public abstract class SmsPortalHubBase : Hub<ISmsPortalHubClient>
{
    /// <summary>
    /// The group that receives conversations that are not owned by any agent or queue (the triage inbox).
    /// </summary>
    public const string UnassignedGroup = "sms:unassigned";

    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISmsAgentPresenceTracker _presenceTracker;
    private readonly ITenantAccessor _tenantAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsPortalHubBase"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the connected agent.</param>
    /// <param name="authorizationService">The authorization service the portal operations are asked through.</param>
    /// <param name="presenceTracker">The tracker that records the connected agent's portal as open.</param>
    /// <param name="tenantAccessor">Names the tenant, so two tenants on one backplane never share a group.</param>
    protected SmsPortalHubBase(
        IAgentProfileManager agentProfileManager,
        IAuthorizationService authorizationService,
        ISmsAgentPresenceTracker presenceTracker,
        ITenantAccessor tenantAccessor)
    {
        _agentProfileManager = agentProfileManager;
        _authorizationService = authorizationService;
        _presenceTracker = presenceTracker;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>
    /// Builds the group name that receives notifications for a single agent's conversations.
    /// </summary>
    /// <param name="agentId">The agent profile id.</param>
    /// <returns>The logical group name.</returns>
    public static string AgentGroup(string agentId) => $"sms:agent:{agentId}";

    /// <summary>
    /// Builds the group name that receives notifications for a queue (department) shared pool.
    /// </summary>
    /// <param name="queueId">The queue id.</param>
    /// <returns>The logical group name.</returns>
    public static string QueueGroup(string queueId) => $"sms:queue:{queueId}";

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        // Refused before anything is joined: a caller who reaches the subscription step is subscribed to other
        // people's conversations, and nothing downstream re-checks. The principal is taken from the request the
        // connection negotiated on rather than from the hub context, which is what the portal has always done.
        if (httpContext?.User is null ||
            !(await _authorizationService.AuthorizeAsync(httpContext.User, (object)null, SmsPortalOperations.UseSmsPortal)).Succeeded)
        {
            Context.Abort();

            return;
        }

        var userId = Context.UserIdentifier;

        if (!string.IsNullOrEmpty(userId))
        {
            var profile = await _agentProfileManager.FindByUserIdAsync(userId, Context.ConnectionAborted);

            if (profile is not null)
            {
                await _presenceTracker.TouchAsync(profile.ItemId, Context.ConnectionAborted);

                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(AgentGroup(profile.ItemId)));

                foreach (var queueId in profile.QueueIds.Concat(profile.AllowedQueueIds).Distinct())
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(QueueGroup(queueId)));
                }
            }

            if ((await _authorizationService.AuthorizeAsync(httpContext.User, (object)null, SmsPortalOperations.ViewAllConversations)).Succeeded)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(UnassignedGroup));
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Records that the connected agent's portal is still open.
    /// </summary>
    /// <remarks>
    /// The page calls this on a timer while it is on screen; when the calls stop, presence lapses and routed
    /// assignment stops pushing conversations at an agent who is no longer there to see them.
    /// </remarks>
    public async Task Heartbeat()
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var profile = await _agentProfileManager.FindByUserIdAsync(userId, Context.ConnectionAborted);

        if (profile is not null)
        {
            await _presenceTracker.TouchAsync(profile.ItemId, Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// Qualifies a logical group name with the tenant it belongs to.
    /// </summary>
    /// <param name="groupName">The logical group name.</param>
    /// <returns>The tenant-qualified group name.</returns>
    private string ForGroup(string groupName)
        => TenantSignalRGroupName.ForGroup(_tenantAccessor.TenantName, groupName);
}
