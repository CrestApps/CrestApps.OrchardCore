using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;

/// <summary>
/// The SignalR hub that powers the real-time messaging workspace. On connect an agent joins their per-agent group and a
/// group for every queue (department) they belong to, so inbound-message and delivery notifications reach the
/// right inboxes. Supervisors who can view all conversations also join the unassigned/triage group. The
/// connection is also the agent's presence: the workspace checks in over it, and routed assignment only pushes
/// conversations at agents whose workspace has checked in recently. The notifications every other admin page carries
/// connect passively: they join the same groups but never count as the workspace being open.
/// </summary>
[Authorize]
public sealed class MessagingHub : Hub<IMessagingHubClient>
{
    /// <summary>
    /// The group that receives conversations that are not owned by any agent or queue (the triage inbox).
    /// </summary>
    public const string UnassignedGroup = "messaging:unassigned";

    /// <summary>
    /// The query string parameter a listening-only connection sets to <c>1</c>. Such a connection joins the same
    /// groups as the workspace but is never recorded as the agent being present.
    /// </summary>
    public const string PassiveQueryParameter = "passive";

    // Marks a passive connection in its items, so a later call over it can tell without re-reading the request it
    // was opened with.
    private static readonly object _passiveConnectionKey = new();

    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;
    private readonly IAuthorizationService _authorizationService;
    private readonly IMessagingPresenceTracker _presenceTracker;
    private readonly ILogger _logger;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingHub"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the connected agent.</param>
    /// <param name="entitlementPolicy">The policy that decides which queues the agent may still serve.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="presenceTracker">The tracker that records the connected agent's workspace as open.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    /// <param name="logger">The logger.</param>
    public MessagingHub(
        IAgentProfileManager agentProfileManager,
        IAgentEntitlementPolicy entitlementPolicy,
        IAuthorizationService authorizationService,
        IMessagingPresenceTracker presenceTracker,
        ShellSettings shellSettings,
        ILogger<MessagingHub> logger)
    {
        _agentProfileManager = agentProfileManager;
        _entitlementPolicy = entitlementPolicy;
        _authorizationService = authorizationService;
        _presenceTracker = presenceTracker;
        _tenantName = shellSettings.Name;
        _logger = logger;
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

        var passive = IsPassive(httpContext.Request);

        if (passive)
        {
            Context.Items[_passiveConnectionKey] = true;
        }

        var userId = Context.UserIdentifier;
        string agentId = null;
        var groupCount = 0;

        if (!string.IsNullOrEmpty(userId))
        {
            var profile = await _agentProfileManager.FindByUserIdAsync(userId, Context.ConnectionAborted);

            if (profile is not null)
            {
                agentId = profile.ItemId;

                // Only the workspace is presence. A passive connection stays open for the toasts while the agent
                // browses other admin pages, and counting it would have routed distribution push conversations at
                // an agent who has no inbox on screen.
                if (!passive)
                {
                    await _presenceTracker.TouchAsync(profile.ItemId, Context.ConnectionAborted);
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(MessagingHub.AgentGroup(profile.ItemId)));
                groupCount++;

                // Only the queues the agent's entitlements still allow, and only for a role that may see its queues'
                // shared inbox: every new message in a queue reaches its group with the customer's address and a
                // preview, and one they may not open is none of theirs.
                if (await _authorizationService.AuthorizeAsync(httpContext.User, MessagingPermissions.ViewQueueConversations))
                {
                    foreach (var queueId in profile.QueueIds.Concat(profile.AllowedQueueIds).Distinct()
                        .Where(queueId => !string.IsNullOrEmpty(queueId) && _entitlementPolicy.AllowsQueue(profile, queueId)))
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(QueueGroup(queueId)));
                        groupCount++;
                    }
                }
            }

            if (await _authorizationService.AuthorizeAsync(httpContext.User, MessagingPermissions.ViewAllConversations))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(UnassignedGroup));
                groupCount++;
            }
        }

        if (groupCount == 0)
        {
            // Allowed to use the workspace, yet tied to no agent, no queue and no triage inbox: nothing this hub sends
            // can reach the connection, which is why such a user sees no toast however many messages arrive.
            _logger.LogWarning(
                "Messaging hub connection {ConnectionId} (passive: {Passive}) has no agent profile and cannot view all conversations, so it joined no group and receives no messaging notifications.",
                Context.ConnectionId.SanitizeLogValue(),
                passive);
        }
        else if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Messaging hub connection {ConnectionId} for agent {AgentId} joined {GroupCount} notification groups (passive: {Passive}).",
                Context.ConnectionId.SanitizeLogValue(),
                agentId.SanitizeLogValue(),
                groupCount,
                passive);
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
        // A passive connection is not the workspace, so it never checks in, even when something calls this over it.
        if (Context.Items.ContainsKey(_passiveConnectionKey))
        {
            return;
        }

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

    private static bool IsPassive(HttpRequest request)
    {
        var value = request.Query[PassiveQueryParameter].ToString();

        return string.Equals(value, "1", StringComparison.Ordinal) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private string ForGroup(string groupName) => TenantSignalRGroupName.ForGroup(_tenantName, groupName);
}
