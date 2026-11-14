using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The search, pickers and rows of the ticket list.
/// </summary>
public sealed class TicketsListViewModel
{
    /// <summary>
    /// Gets or sets the search text: a ticket number or words of the subject.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the saved view the list starts from.
    /// </summary>
    public string ViewId { get; set; }

    /// <summary>
    /// Gets or sets the state picker: open, solved, closed or all.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the status picker.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the desk picker.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the priority picker.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the team picker.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the assignee picker: <see cref="TicketAssignees.Me"/>, <see cref="TicketAssignees.Unassigned"/> or a
    /// user id.
    /// </summary>
    public string Assignee { get; set; }

    /// <summary>
    /// Gets or sets the ticket type picker.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the service-level picker: breached or soon.
    /// </summary>
    public string Due { get; set; }

    /// <summary>
    /// Gets or sets the sort.
    /// </summary>
    public string Sort { get; set; }

    /// <summary>
    /// Gets or sets the options of the pickers.
    /// </summary>
    [BindNever]
    public TicketsListOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the bulk actions offered.
    /// </summary>
    [BindNever]
    public List<SelectListItem> BulkActions { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext<TicketRow> List { get; set; }
}

/// <summary>
/// The options of the ticket list's pickers.
/// </summary>
public sealed class TicketsListOptions
{
    /// <summary>
    /// Gets or sets the saved views the user may use.
    /// </summary>
    public List<SelectListItem> Views { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public List<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the desks the user may view.
    /// </summary>
    public List<SelectListItem> Desks { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities.
    /// </summary>
    public List<SelectListItem> Priorities { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    public List<SelectListItem> Teams { get; set; } = [];

    /// <summary>
    /// Gets or sets the ticket types.
    /// </summary>
    public List<SelectListItem> Types { get; set; } = [];
}

/// <summary>
/// A row of the ticket list.
/// </summary>
public sealed class TicketRow
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public ContentItem Ticket { get; set; }

    /// <summary>
    /// Gets or sets the ticket's part.
    /// </summary>
    public TicketPart Part { get; set; }

    /// <summary>
    /// Gets or sets its status.
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Gets or sets its priority.
    /// </summary>
    public TicketPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets its desk's name.
    /// </summary>
    public string DeskName { get; set; }

    /// <summary>
    /// Gets or sets its team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets its assignee's name.
    /// </summary>
    public string AssigneeName { get; set; }

    /// <summary>
    /// Gets or sets its requester's name.
    /// </summary>
    public string RequesterName { get; set; }

    /// <summary>
    /// Gets or sets the time the list was built.
    /// </summary>
    public DateTime NowUtc { get; set; }
}

/// <summary>
/// The new-ticket page: the ticket type's editor and the ticket's own values.
/// </summary>
public sealed class TicketCreateViewModel
{
    /// <summary>
    /// Gets or sets the ticket type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the first message.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the desk.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the requester, as <c>Kind:Id</c>.
    /// </summary>
    public string Requester { get; set; }

    /// <summary>
    /// Gets or sets the requester's name, for one no party source knows.
    /// </summary>
    public string RequesterName { get; set; }

    /// <summary>
    /// Gets or sets the requester's e-mail, for one no party source knows.
    /// </summary>
    public string RequesterEmail { get; set; }

    /// <summary>
    /// Gets or sets the account the ticket is for.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the ticket types the user may create.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the desks the user may create tickets in.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Desks { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Priorities { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor of the ticket type's fields.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }

    /// <summary>
    /// Gets or sets whether files can be attached.
    /// </summary>
    [BindNever]
    public bool CanAttach { get; set; }
}

/// <summary>
/// The ticket page.
/// </summary>
public sealed class TicketPageViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public ContentItem Ticket { get; set; }

    /// <summary>
    /// Gets or sets the ticket's part.
    /// </summary>
    public TicketPart Part { get; set; }

    /// <summary>
    /// Gets or sets its status.
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Gets or sets its priority.
    /// </summary>
    public TicketPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the ticket's display, built by the display drivers: its fields and the panels other modules add.
    /// </summary>
    public IShape Display { get; set; }

    /// <summary>
    /// Gets or sets the statuses a reply can set.
    /// </summary>
    public List<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may reply to the requester.
    /// </summary>
    public bool CanReply { get; set; }

    /// <summary>
    /// Gets or sets whether the user may add internal notes.
    /// </summary>
    public bool CanNote { get; set; }

    /// <summary>
    /// Gets or sets whether the user may edit the ticket's fields.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets whether the user may merge the ticket into another.
    /// </summary>
    public bool CanMerge { get; set; }

    /// <summary>
    /// Gets or sets whether the user may delete the ticket.
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Gets or sets whether files can be attached.
    /// </summary>
    public bool CanAttach { get; set; }

    /// <summary>
    /// Gets or sets whether the ticket can't change any more (closed or merged).
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets the named actions the user can run on the ticket (workflows that start with Ticket Action Requested).
    /// </summary>
    public IReadOnlyList<string> Actions { get; set; } = [];

    /// <summary>
    /// Gets or sets the reply templates the composer offers, filled in for the ticket.
    /// </summary>
    public IReadOnlyList<TicketReplyTemplate> ReplyTemplates { get; set; } = [];

    /// <summary>
    /// Gets or sets the text a refused reply kept.
    /// </summary>
    public string Draft { get; set; }

    /// <summary>
    /// Gets or sets the ticket this one was merged into.
    /// </summary>
    public ContentItem MergedInto { get; set; }

    /// <summary>
    /// Gets or sets the closed ticket this one follows up.
    /// </summary>
    public ContentItem FollowUpOf { get; set; }
}

/// <summary>
/// The ticket's values the properties panel edits.
/// </summary>
public class TicketPropertiesViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public ContentItem Ticket { get; set; }

    /// <summary>
    /// Gets or sets the ticket's part.
    /// </summary>
    public TicketPart Part { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    public List<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities.
    /// </summary>
    public List<SelectListItem> Priorities { get; set; } = [];

    /// <summary>
    /// Gets or sets the desks.
    /// </summary>
    public List<SelectListItem> Desks { get; set; } = [];

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the assignee's name.
    /// </summary>
    public string AssigneeName { get; set; }

    /// <summary>
    /// Gets or sets whether the user may change the status, priority and desk.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets whether the user may change the team and the assignee.
    /// </summary>
    public bool CanAssign { get; set; }

    /// <summary>
    /// Gets or sets whether the ticket can't change any more.
    /// </summary>
    public bool IsReadOnly { get; set; }
}

/// <summary>
/// The requester panel of the ticket page.
/// </summary>
public class TicketRequesterViewModel
{
    /// <summary>
    /// Gets or sets the ticket's part.
    /// </summary>
    public TicketPart Part { get; set; }

    /// <summary>
    /// Gets or sets the requester's name.
    /// </summary>
    public string RequesterName { get; set; }

    /// <summary>
    /// Gets or sets the requester's other tickets, newest first.
    /// </summary>
    public List<TicketRow> PreviousTickets { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of the requester's other tickets.
    /// </summary>
    public int PreviousTotal { get; set; }

    /// <summary>
    /// Gets or sets the participants' names.
    /// </summary>
    public List<string> Participants { get; set; } = [];
}

/// <summary>
/// The service-level panel of the ticket page.
/// </summary>
public class TicketServiceLevelsViewModel
{
    /// <summary>
    /// Gets or sets the ticket's timers, newest first.
    /// </summary>
    public List<CrestApps.OrchardCore.ServiceLevels.Models.ServiceLevelTimer> Timers { get; set; } = [];

    /// <summary>
    /// Gets or sets the time the page was built.
    /// </summary>
    public DateTime NowUtc { get; set; }
}

/// <summary>
/// The links panel of the ticket page.
/// </summary>
public class TicketLinksViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public ContentItem Ticket { get; set; }

    /// <summary>
    /// Gets or sets the links with their display names and addresses.
    /// </summary>
    public List<TicketLinkView> Links { get; set; } = [];

    /// <summary>
    /// Gets or sets the record kinds a link can point at.
    /// </summary>
    public List<SelectListItem> Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may add links.
    /// </summary>
    public bool CanEdit { get; set; }
}

/// <summary>
/// A link of a ticket, as the links panel shows it.
/// </summary>
public sealed class TicketLinkView
{
    /// <summary>
    /// Gets or sets the link.
    /// </summary>
    public TicketLink Link { get; set; }

    /// <summary>
    /// Gets or sets the linked record's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the linked record's kind name.
    /// </summary>
    public string KindName { get; set; }

    /// <summary>
    /// Gets or sets the linked record's admin address, if any.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// What using a reply template on a ticket also changes.
/// </summary>
public class TicketMacroViewModel
{
    /// <summary>
    /// Gets or sets the status the ticket moves to.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the priority the ticket gets.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the team the ticket goes to.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Priorities { get; set; } = [];
}
