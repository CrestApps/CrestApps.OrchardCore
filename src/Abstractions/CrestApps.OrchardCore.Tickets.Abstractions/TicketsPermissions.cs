using System.Collections.Concurrent;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// The permissions of tickets. Each ticket permission also exists for every desk, so a desk's tickets, such as HR's,
/// stay with the people given that desk. Viewing comes with a scope: all tickets, the tickets of the user's teams, or
/// the tickets assigned to them; every other ticket permission applies to the tickets the user may view.
/// </summary>
public static class TicketsPermissions
{
    /// <summary>
    /// Manage statuses, priorities, desks and shared views.
    /// </summary>
    public static readonly Permission ManageTicketSettings = new(
        "ManageTicketSettings",
        LocalizationSource.Create("Manage ticket settings", typeof(TicketsPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Create, arrange and delete ticket boards shared with everybody or a team. A board only ever shows the tickets its
    /// viewer may see.
    /// </summary>
    public static readonly Permission ManageTicketBoards = new(
        "ManageTicketBoards",
        LocalizationSource.Create("Manage shared ticket boards", typeof(TicketsPermissions)),
        [ManageTicketSettings]);

    /// <summary>
    /// Create, arrange and delete the user's own, personal ticket boards.
    /// </summary>
    public static readonly Permission ManageOwnTicketBoards = new(
        "ManageOwnTicketBoards",
        LocalizationSource.Create("Manage my own ticket boards", typeof(TicketsPermissions)),
        [ManageTicketBoards]);

    /// <summary>
    /// View every ticket.
    /// </summary>
    public static readonly Permission ViewAllTickets = new(
        "ViewAllTickets",
        LocalizationSource.Create("View all tickets", typeof(TicketsPermissions)));

    /// <summary>
    /// View the tickets of the user's teams and those assigned to them.
    /// </summary>
    public static readonly Permission ViewTeamTickets = new(
        "ViewTeamTickets",
        LocalizationSource.Create("View the tickets of my teams", typeof(TicketsPermissions)),
        [ViewAllTickets]);

    /// <summary>
    /// View the tickets assigned to the user.
    /// </summary>
    public static readonly Permission ViewAssignedTickets = new(
        "ViewAssignedTickets",
        LocalizationSource.Create("View the tickets assigned to me", typeof(TicketsPermissions)),
        [ViewTeamTickets]);

    /// <summary>
    /// Change the properties of tickets the user may view.
    /// </summary>
    public static readonly Permission EditTickets = new(
        "EditTickets",
        LocalizationSource.Create("Edit tickets", typeof(TicketsPermissions)));

    /// <summary>
    /// Send public replies on tickets the user may view.
    /// </summary>
    public static readonly Permission ReplyToTickets = new(
        "ReplyToTickets",
        LocalizationSource.Create("Reply to tickets", typeof(TicketsPermissions)),
        [EditTickets]);

    /// <summary>
    /// Add internal notes to tickets the user may view.
    /// </summary>
    public static readonly Permission AddTicketNotes = new(
        "AddTicketNotes",
        LocalizationSource.Create("Add internal notes to tickets", typeof(TicketsPermissions)),
        [ReplyToTickets]);

    /// <summary>
    /// Assign tickets the user may view.
    /// </summary>
    public static readonly Permission AssignTickets = new(
        "AssignTickets",
        LocalizationSource.Create("Assign tickets", typeof(TicketsPermissions)));

    /// <summary>
    /// Merge tickets the user may view.
    /// </summary>
    public static readonly Permission MergeTickets = new(
        "MergeTickets",
        LocalizationSource.Create("Merge tickets", typeof(TicketsPermissions)));

    /// <summary>
    /// Delete tickets the user may view.
    /// </summary>
    public static readonly Permission DeleteTickets = new(
        "DeleteTickets",
        LocalizationSource.Create("Delete tickets", typeof(TicketsPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Call off a ticket's pending approval, so it goes back to work without its approvers, such as when an approver
    /// left. The cancellation is on the ticket's timeline.
    /// </summary>
    public static readonly Permission CancelTicketApprovals = new(
        "CancelTicketApprovals",
        LocalizationSource.Create("Cancel ticket approvals", typeof(TicketsPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Open the ticket reports. Each report shows only the tickets the reader may view.
    /// </summary>
    public static readonly Permission ViewTicketReports = new(
        "ViewTicketReports",
        LocalizationSource.Create("View ticket reports", typeof(TicketsPermissions)));

    private static readonly PermissionTemplate _viewAll = new("ViewAllTickets_{0}", LocalizationSource.Create("View all tickets in {0}", typeof(TicketsPermissions)), ViewAllTickets);

    private static readonly PermissionTemplate _viewTeam = new("ViewTeamTickets_{0}", LocalizationSource.Create("View the tickets of my teams in {0}", typeof(TicketsPermissions)), ViewTeamTickets)
    {
        ImpliedByTemplates = [_viewAll],
    };

    private static readonly PermissionTemplate _viewAssigned = new("ViewAssignedTickets_{0}", LocalizationSource.Create("View the tickets assigned to me in {0}", typeof(TicketsPermissions)), ViewAssignedTickets)
    {
        ImpliedByTemplates = [_viewTeam],
    };

    private static readonly PermissionTemplate _edit = new("EditTickets_{0}", LocalizationSource.Create("Edit tickets in {0}", typeof(TicketsPermissions)), EditTickets);

    private static readonly PermissionTemplate _reply = new("ReplyToTickets_{0}", LocalizationSource.Create("Reply to tickets in {0}", typeof(TicketsPermissions)), ReplyToTickets)
    {
        ImpliedByTemplates = [_edit],
    };

    private static readonly PermissionTemplate _note = new("AddTicketNotes_{0}", LocalizationSource.Create("Add internal notes to tickets in {0}", typeof(TicketsPermissions)), AddTicketNotes)
    {
        ImpliedByTemplates = [_reply],
    };

    private static readonly PermissionTemplate _assign = new("AssignTickets_{0}", LocalizationSource.Create("Assign tickets in {0}", typeof(TicketsPermissions)), AssignTickets);

    private static readonly PermissionTemplate _merge = new("MergeTickets_{0}", LocalizationSource.Create("Merge tickets in {0}", typeof(TicketsPermissions)), MergeTickets);

    private static readonly PermissionTemplate _delete = new("DeleteTickets_{0}", LocalizationSource.Create("Delete tickets in {0}", typeof(TicketsPermissions)), DeleteTickets)
    {
        IsSecurityCritical = true,
    };

    private static readonly Dictionary<TicketOperation, PermissionTemplate> _templates = new()
    {
        [TicketOperation.ViewAll] = _viewAll,
        [TicketOperation.ViewTeam] = _viewTeam,
        [TicketOperation.ViewAssigned] = _viewAssigned,
        [TicketOperation.Edit] = _edit,
        [TicketOperation.Reply] = _reply,
        [TicketOperation.Note] = _note,
        [TicketOperation.Assign] = _assign,
        [TicketOperation.Merge] = _merge,
        [TicketOperation.Delete] = _delete,
    };

    // Keyed by the desk's name too: the cache is shared by every tenant of the process, and tenants set up from the same
    // recipe have desks with the same id but maybe another name, as a desk renamed later has.
    private static readonly ConcurrentDictionary<(TicketOperation, string, string), Permission> _deskPermissions = new();

    /// <summary>
    /// Gets the operations that have a permission per desk.
    /// </summary>
    public static IReadOnlyCollection<TicketOperation> DeskOperations => _templates.Keys;

    /// <summary>
    /// Returns the permission of an operation on a desk's tickets.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="desk">The desk.</param>
    /// <returns>The permission.</returns>
    public static Permission ForDesk(TicketOperation operation, TicketDesk desk)
    {
        ArgumentNullException.ThrowIfNull(desk);

        var name = desk.Name ?? desk.ItemId;

        return _deskPermissions.GetOrAdd((operation, desk.ItemId, name), key => _templates[key.Item1].CreateDynamicPermission(key.Item2, key.Item3));
    }
}
