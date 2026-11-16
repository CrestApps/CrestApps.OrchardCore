using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tickets.RealTime;

/// <summary>
/// Tells open ticket boards that tickets changed, so they load their cards again at once instead of at their next poll.
/// The message carries nothing about the ticket: each board reloads what its reader may see.
/// </summary>
[Authorize]
public sealed class TicketBoardHub : Hub
{
    private readonly ITicketAccessService _access;
    private readonly string _tenantName;

    public TicketBoardHub(ITicketAccessService access, ShellSettings shellSettings)
    {
        _access = access;
        _tenantName = shellSettings.Name;
    }

    /// <summary>
    /// Joins the boards' updates, for users who may see the tickets of at least one desk.
    /// </summary>
    /// <returns>Whether the connection joined.</returns>
    public async Task<bool> Subscribe()
    {
        if ((await _access.GetViewScopesAsync(Context.User, Context.ConnectionAborted)).Count == 0)
        {
            return false;
        }

        // A membership change must not stop half way, so it never takes the connection's token.
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(_tenantName), HubConnectionWork.MustComplete);

        return true;
    }

    /// <summary>
    /// Gives the tenant's board group.
    /// </summary>
    /// <param name="tenantName">The tenant.</param>
    /// <returns>The group's name.</returns>
    public static string Group(string tenantName) => TenantSignalRGroupName.ForGroup(tenantName, "ticket-boards");
}

/// <summary>
/// Pings the open boards once a ticket change is saved: a new, deleted or merged ticket, another status or assignee, and
/// any other change of its values.
/// </summary>
public sealed class TicketBoardNotifier : ITicketObserver
{
    /// <summary>
    /// The client method boards listen to.
    /// </summary>
    public const string ClientMethod = "TicketsChanged";

    private const string WorkName = "ticket_board_notification";

    private readonly string _tenantName;
    private bool _queued;

    public TicketBoardNotifier(ShellSettings shellSettings)
    {
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default) => QueueAsync();

    /// <inheritdoc/>
    public Task UpdatedAsync(ContentItem ticket, IReadOnlyList<TimelineFieldChange> changes, CancellationToken cancellationToken = default) => QueueAsync();

    /// <inheritdoc/>
    public Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default) => QueueAsync();

    /// <inheritdoc/>
    public Task StatusChangedAsync(ContentItem ticket, Models.TicketStatus previous, Models.TicketStatus current, CancellationToken cancellationToken = default) => QueueAsync();

    /// <inheritdoc/>
    public Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default) => QueueAsync();

    /// <inheritdoc/>
    public Task DeletedAsync(ContentItem ticket, CancellationToken cancellationToken = default) => QueueAsync();

    // One ping per request, after it commits, however many tickets it changed (a bulk action changes several).
    private Task QueueAsync()
    {
        if (!_queued)
        {
            _queued = true;
            var tenantName = _tenantName;

            DeferredWork.Add(WorkName, scope => scope.ServiceProvider.GetRequiredService<IHubContext<TicketBoardHub>>().Clients.Group(TicketBoardHub.Group(tenantName)).SendAsync(ClientMethod));
        }

        return Task.CompletedTask;
    }
}
