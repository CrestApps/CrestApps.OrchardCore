using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Reports.Designer.RealTime;

/// <summary>
/// The hub of the report builder. A builder page subscribes to the report it edits, receives the report's changes (see
/// <see cref="SignalRReportDesignNotifier"/>), and tells the other people there that it arrived or left. The server
/// only relays these presence messages and keeps no list, so it works on several nodes behind a backplane.
/// </summary>
[Authorize]
public sealed class ReportsHub : Hub
{
    // The reports this connection subscribed to, so leaving tells their groups.
    private const string SubscriptionsKey = "CrestApps.Reports.Subscriptions";

    private readonly ReportDesignService _designService;
    private readonly IAuthorizationService _authorizationService;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsHub"/> class.
    /// </summary>
    /// <param name="designService">The design service used to find the report.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellSettings">The current tenant.</param>
    public ReportsHub(
        ReportDesignService designService,
        IAuthorizationService authorizationService,
        ShellSettings shellSettings)
    {
        _designService = designService;
        _authorizationService = authorizationService;
        _tenantName = shellSettings.Name;
    }

    /// <summary>
    /// Subscribes to the changes of a report the user may edit, and tells the others there that the user arrived.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns><see langword="true"/> when subscribed.</returns>
    public async Task<bool> Subscribe(string designId)
    {
        if (string.IsNullOrEmpty(designId) || !await CanEditAsync(designId))
        {
            return false;
        }

        var group = Group(_tenantName, designId);

        // A membership change must not stop half way, so it never takes the connection's token.
        await Groups.AddToGroupAsync(Context.ConnectionId, group, HubConnectionWork.MustComplete);
        Subscriptions.Add(designId);
        await Clients.OthersInGroup(group).SendAsync("PresenceJoined", CurrentUser());

        return true;
    }

    /// <summary>
    /// Stops the changes of a report, and tells the others there that the user left.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Unsubscribe(string designId)
    {
        if (string.IsNullOrEmpty(designId) || !Subscriptions.Remove(designId))
        {
            return;
        }

        var group = Group(_tenantName, designId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, HubConnectionWork.MustComplete);
        await Clients.Group(group).SendAsync("PresenceLeft", Context.ConnectionId);
    }

    /// <summary>
    /// Answers someone who just arrived at a report this connection is subscribed to: tells them this user is there.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <param name="connectionId">The connection of the person who arrived.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AnnouncePresence(string designId, string connectionId)
    {
        if (string.IsNullOrEmpty(designId) || string.IsNullOrEmpty(connectionId) || !Subscriptions.Contains(designId))
        {
            return Task.CompletedTask;
        }

        return Clients.Client(connectionId).SendAsync("PresenceHere", CurrentUser());
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        foreach (var designId in Subscriptions)
        {
            await Clients.Group(Group(_tenantName, designId)).SendAsync("PresenceLeft", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// The group of the people who have a report open, qualified by tenant so tenants sharing a backplane stay apart.
    /// </summary>
    /// <param name="tenantName">The tenant name.</param>
    /// <param name="designId">The report identifier.</param>
    /// <returns>The group name.</returns>
    public static string Group(string tenantName, string designId)
    {
        return TenantSignalRGroupName.ForGroup(tenantName, "report-design:" + designId);
    }

    private HashSet<string> Subscriptions
    {
        get
        {
            if (Context.Items.TryGetValue(SubscriptionsKey, out var value) && value is HashSet<string> subscriptions)
            {
                return subscriptions;
            }

            subscriptions = new HashSet<string>(StringComparer.Ordinal);
            Context.Items[SubscriptionsKey] = subscriptions;

            return subscriptions;
        }
    }

    private async Task<bool> CanEditAsync(string designId)
    {
        var design = await _designService.FindAsync(designId);

        return design is not null &&
            await _authorizationService.AuthorizeAsync(Context.User, ReportDesignerPermissions.ManageAllReportDesigns, design);
    }

    private ReportPresence CurrentUser()
    {
        return new ReportPresence
        {
            ConnectionId = Context.ConnectionId,
            UserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = Context.User?.Identity?.Name,
        };
    }
}

/// <summary>
/// Someone who has a report open in the builder.
/// </summary>
public sealed class ReportPresence
{
    /// <summary>
    /// Gets or sets the connection, which identifies one open page.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    public string UserName { get; set; }
}
