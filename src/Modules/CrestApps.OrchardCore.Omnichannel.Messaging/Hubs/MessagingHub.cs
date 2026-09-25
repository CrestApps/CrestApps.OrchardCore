using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;

/// <summary>
/// The SignalR hub that powers the real-time messaging workspace. On connect an agent joins their per-agent group and a
/// group for every queue (department) they belong to, so inbound-message and delivery notifications reach the
/// right inboxes. Supervisors who can view all conversations also join the unassigned/triage group. The
/// connection is also the agent's presence: the workspace checks in over it, and routed assignment only pushes
/// conversations at agents whose workspace has checked in recently.
/// </summary>
[Authorize]
public sealed class MessagingHub : Hub<IMessagingHubClient>
{
    /// <summary>
    /// The group that receives conversations that are not owned by any agent or queue (the triage inbox).
    /// </summary>
    public const string UnassignedGroup = "messaging:unassigned";

    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IMessagingPresenceTracker _presenceTracker;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingHub"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the connected agent.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="presenceTracker">The tracker that records the connected agent's workspace as open.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public MessagingHub(
        IAgentProfileManager agentProfileManager,
        IAuthorizationService authorizationService,
        IMessagingPresenceTracker presenceTracker,
        ShellSettings shellSettings)
    {
        _agentProfileManager = agentProfileManager;
        _authorizationService = authorizationService;
        _presenceTracker = presenceTracker;
        _tenantName = shellSettings.Name;
    }

    /// <summary>
    /// Builds the group name that receives notifications for a single agent's conversations.
    /// </summary>
    /// <param name="agentId">The agent profile id.</param>
    public static string AgentGroup(string agentId) => $"messaging:agent:{agentId}";

    /// <summary>
    /// Builds the group name that receives notifications for a queue (department) shared pool.
    /// </summary>
    /// <param name="queueId">The queue id.</param>
    public static string QueueGroup(string queueId) => $"messaging:queue:{queueId}";

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        if (httpContext?.User is null ||
            !await _authorizationService.AuthorizeAsync(httpContext.User, MessagingPermissions.UseMessagingWorkspace))
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

                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(MessagingHub.AgentGroup(profile.ItemId)));

                foreach (var queueId in profile.QueueIds.Concat(profile.AllowedQueueIds).Distinct())
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(QueueGroup(queueId)));
                }
            }

            if (await _authorizationService.AuthorizeAsync(httpContext.User, MessagingPermissions.ViewAllConversations))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(UnassignedGroup));
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Records that the connected agent's workspace is still open. The page calls this on a timer while it is on
    /// screen; when the calls stop, presence lapses and routed assignment stops pushing conversations at an
    /// agent who is no longer there to see them.
    /// </summary>
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

    private string ForGroup(string groupName) => TenantSignalRGroupName.ForGroup(_tenantName, groupName);
}
