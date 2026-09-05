using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Sms.Workspace.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Sms.Workspace.Hubs;

/// <summary>
/// The SignalR hub that powers the real-time SMS portal. On connect an agent joins their per-agent group and a
/// group for every queue (department) they belong to, so inbound-message and delivery notifications reach the
/// right inboxes. Supervisors who can view all conversations also join the unassigned/triage group.
/// </summary>
[Authorize]
public sealed class SmsPortalHub : Hub<ISmsPortalHubClient>
{
    /// <summary>
    /// The group that receives conversations that are not owned by any agent or queue (the triage inbox).
    /// </summary>
    public const string UnassignedGroup = "sms:unassigned";

    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsPortalHub"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the connected agent.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public SmsPortalHub(
        IAgentProfileManager agentProfileManager,
        IAuthorizationService authorizationService,
        ShellSettings shellSettings)
    {
        _agentProfileManager = agentProfileManager;
        _authorizationService = authorizationService;
        _tenantName = shellSettings.Name;
    }

    /// <summary>
    /// Builds the group name that receives notifications for a single agent's conversations.
    /// </summary>
    /// <param name="agentId">The agent profile id.</param>
    public static string AgentGroup(string agentId) => $"sms:agent:{agentId}";

    /// <summary>
    /// Builds the group name that receives notifications for a queue (department) shared pool.
    /// </summary>
    /// <param name="queueId">The queue id.</param>
    public static string QueueGroup(string queueId) => $"sms:queue:{queueId}";

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        if (httpContext?.User is null ||
            !await _authorizationService.AuthorizeAsync(httpContext.User, SmsWorkspacePermissions.UseSmsPortal))
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
                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(SmsPortalHub.AgentGroup(profile.ItemId)));

                foreach (var queueId in profile.QueueIds.Concat(profile.AllowedQueueIds).Distinct())
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(QueueGroup(queueId)));
                }
            }

            if (await _authorizationService.AuthorizeAsync(httpContext.User, SmsWorkspacePermissions.ViewAllConversations))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ForGroup(UnassignedGroup));
            }
        }

        await base.OnConnectedAsync();
    }

    private string ForGroup(string groupName) => TenantSignalRGroupName.ForGroup(_tenantName, groupName);
}
