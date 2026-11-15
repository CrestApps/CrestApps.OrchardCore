using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;

namespace CrestApps.OrchardCore.Tickets.Controllers;

/// <summary>
/// Turns the ticket list's search and pickers into a query. A saved view is the starting point; each picker that has a
/// value narrows or replaces the matching part of it, and the saved view itself is never changed.
/// </summary>
internal static class TicketListQueries
{
    /// <summary>
    /// How far ahead "due soon" looks.
    /// </summary>
    public static readonly TimeSpan SoonWindow = TimeSpan.FromHours(4);

    private static readonly TicketStatusCategory[] _open = [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold];

    /// <summary>
    /// Builds the query of the list.
    /// </summary>
    /// <param name="form">The search and pickers.</param>
    /// <param name="view">The saved view the list starts from, if any.</param>
    /// <returns>The query, without paging.</returns>
    public static TicketQuery Build(TicketsListViewModel form, TicketView view)
    {
        ArgumentNullException.ThrowIfNull(form);

        var start = view?.Query;
        var query = new TicketQuery
        {
            Search = start?.Search,
            DeskIds = [.. start?.DeskIds ?? []],
            Categories = [.. start?.Categories ?? []],
            StatusIds = [.. start?.StatusIds ?? []],
            PriorityIds = [.. start?.PriorityIds ?? []],
            TeamIds = [.. start?.TeamIds ?? []],
            Assignee = start?.Assignee,
            ContentTypes = [.. start?.ContentTypes ?? []],
            Requester = start?.Requester,
            AccountId = start?.AccountId,
            DueWithin = start?.DueWithin,
            Sort = start?.Sort ?? TicketSort.Updated,
        };

        if (!string.IsNullOrWhiteSpace(form.Q))
        {
            query.Search = form.Q.Trim();
        }

        if (!string.IsNullOrEmpty(form.StatusId))
        {
            // One status says more than a state.
            query.StatusIds = [form.StatusId];
            query.Categories = [];
        }
        else if (!string.IsNullOrEmpty(form.State))
        {
            query.Categories = form.State switch
            {
                States.Solved => [TicketStatusCategory.Solved],
                States.Closed => [TicketStatusCategory.Closed],
                States.All => [],
                _ => [.. _open],
            };
        }
        else if (view is null)
        {
            query.Categories = [.. _open];
        }

        if (!string.IsNullOrEmpty(form.DeskId))
        {
            query.DeskIds = [form.DeskId];
        }

        if (!string.IsNullOrEmpty(form.AccountId))
        {
            query.AccountId = form.AccountId;
        }

        if (!string.IsNullOrEmpty(form.PriorityId))
        {
            query.PriorityIds = [form.PriorityId];
        }

        if (!string.IsNullOrEmpty(form.TeamId))
        {
            query.TeamIds = [form.TeamId];
        }

        if (!string.IsNullOrEmpty(form.ContentType))
        {
            query.ContentTypes = [form.ContentType];
        }

        if (!string.IsNullOrEmpty(form.Assignee))
        {
            query.Assignee = form.Assignee;
        }

        query.DueWithin = form.Due switch
        {
            DueFilters.Breached => TimeSpan.Zero,
            DueFilters.Soon => SoonWindow,
            _ => query.DueWithin,
        };

        if (Enum.TryParse<TicketSort>(form.Sort, ignoreCase: true, out var sort))
        {
            query.Sort = sort;
        }

        return query;
    }

    /// <summary>
    /// The values of the state picker.
    /// </summary>
    public static class States
    {
        /// <summary>
        /// New, open, pending and on-hold tickets.
        /// </summary>
        public const string Open = "open";

        /// <summary>
        /// Solved tickets.
        /// </summary>
        public const string Solved = "solved";

        /// <summary>
        /// Closed tickets.
        /// </summary>
        public const string Closed = "closed";

        /// <summary>
        /// Every ticket.
        /// </summary>
        public const string All = "all";
    }

    /// <summary>
    /// The values of the service-level picker.
    /// </summary>
    public static class DueFilters
    {
        /// <summary>
        /// Tickets whose next target has passed.
        /// </summary>
        public const string Breached = "breached";

        /// <summary>
        /// Tickets whose next target is within <see cref="SoonWindow"/>.
        /// </summary>
        public const string Soon = "soon";
    }
}
