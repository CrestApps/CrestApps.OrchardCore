using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// Who a board is for.
/// </summary>
public enum TicketBoardSharing
{
    /// <summary>
    /// Only the user who made it.
    /// </summary>
    Personal,

    /// <summary>
    /// The members of a team.
    /// </summary>
    Team,

    /// <summary>
    /// Everybody who may view tickets.
    /// </summary>
    Everybody,
}

/// <summary>
/// The editor of a board.
/// </summary>
public class TicketBoardViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the board is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets who the board is for.
    /// </summary>
    public TicketBoardSharing Sharing { get; set; }

    /// <summary>
    /// Gets or sets the team it is shared with.
    /// </summary>
    public string SharedWithTeamId { get; set; }

    /// <summary>
    /// Gets or sets where it is listed.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the desks whose tickets it shows; none shows every desk the viewer may see.
    /// </summary>
    public IList<string> DeskIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams whose tickets it shows.
    /// </summary>
    public IList<string> TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ticket types it shows.
    /// </summary>
    public IList<string> ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets whose tickets it shows.
    /// </summary>
    public string Assignee { get; set; }

    /// <summary>
    /// Gets or sets the order of the cards.
    /// </summary>
    public TicketSort Sort { get; set; }

    /// <summary>
    /// Gets or sets the columns, from left to right.
    /// </summary>
    public IList<TicketBoardColumnViewModel> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the desks to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Desks { get; set; } = [];

    /// <summary>
    /// Gets or sets the ticket types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses to choose from, in the order of the status list.
    /// </summary>
    [BindNever]
    public IList<TicketStatus> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may share boards with everybody or a team.
    /// </summary>
    [BindNever]
    public bool CanShare { get; set; }
}

/// <summary>
/// A column in the board editor.
/// </summary>
public class TicketBoardColumnViewModel
{
    /// <summary>
    /// Gets or sets the column's id; empty for a new column.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public IList<string> StatusIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the status a dropped card gets; empty asks.
    /// </summary>
    public string DefaultStatusId { get; set; }

    /// <summary>
    /// Gets or sets the most tickets it should hold.
    /// </summary>
    public int? Limit { get; set; }
}

/// <summary>
/// The list of boards.
/// </summary>
public class TicketBoardsIndexViewModel
{
    /// <summary>
    /// Gets or sets the boards the user may open.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets whether the user may create boards.
    /// </summary>
    [BindNever]
    public bool CanCreate { get; set; }
}

/// <summary>
/// The page of a board.
/// </summary>
public class TicketBoardPageViewModel
{
    /// <summary>
    /// Gets or sets the board.
    /// </summary>
    public TicketBoard Board { get; set; }

    /// <summary>
    /// Gets or sets the other boards the user may open.
    /// </summary>
    public IList<TicketBoard> Boards { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may arrange the board.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the address of the hub that pings the board when tickets change, when SignalR is on.
    /// </summary>
    public string HubUrl { get; set; }
}

/// <summary>
/// The actions of a board's row.
/// </summary>
public class TicketBoardRowViewModel
{
    /// <summary>
    /// Gets or sets the board.
    /// </summary>
    public TicketBoard Board { get; set; }

    /// <summary>
    /// Gets or sets whether the user may arrange or delete it.
    /// </summary>
    public bool CanManage { get; set; }
}
