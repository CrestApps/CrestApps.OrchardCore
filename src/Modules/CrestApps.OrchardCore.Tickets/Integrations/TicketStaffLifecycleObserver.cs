using CrestApps.Core.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tickets.Integrations;

/// <summary>
/// When a staff member leaves (or their user is unlinked), the tickets still being worked on that were assigned to their
/// user go back to their team's queue: unassigned, with an event on each ticket saying why.
/// </summary>
internal sealed class TicketStaffLifecycleObserver : IStaffLifecycleObserver
{
    // The most tickets one change hands back; a larger backlog is unusual and the rest can be found in the list.
    private const int MaxTickets = 1000;

    private readonly ITicketService _tickets;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public TicketStaffLifecycleObserver(ITicketService tickets, ICatalog<TicketDesk> desks, ILogger<TicketStaffLifecycleObserver> logger, IStringLocalizer<TicketStaffLifecycleObserver> stringLocalizer)
    {
        _tickets = tickets;
        _desks = desks;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The user whose tickets go back: the one of a member who left, or the one a member no longer has.
        var userId = context.Terminated
            ? context.StaffMember?.UserId ?? context.PreviousUserId
            : context.UserChanged && !context.IsNew ? context.PreviousUserId : null;

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var desks = (await _desks.GetAllAsync(cancellationToken)).Select(desk => desk.ItemId).ToList();
        var (tickets, _) = await _tickets.ListAsync(new TicketScopedQuery
        {
            Query = new TicketQuery
            {
                Assignee = userId,
                Categories = [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold],
                Take = MaxTickets,
            },
            AllDeskIds = desks,
        }, cancellationToken);

        var actor = new TicketActor { Name = S["Staff change"] };

        foreach (var ticket in tickets)
        {
            var result = await _tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate { AssigneeId = string.Empty }, actor, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("A ticket of a staff member who left couldn't be unassigned: {Errors}", string.Join(" ", result.Errors));
            }
        }
    }
}
