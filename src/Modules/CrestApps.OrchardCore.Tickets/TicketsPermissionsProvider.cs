using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// Lists the ticket permissions: the ones over every desk, and a set for each desk.
/// </summary>
internal sealed class TicketsPermissionsProvider : IPermissionProvider
{
    private static readonly Permission[] _global =
    [
        TicketsPermissions.ManageTicketSettings,
        TicketsPermissions.ManageTicketBoards,
        TicketsPermissions.ManageOwnTicketBoards,
        TicketsPermissions.ViewAllTickets,
        TicketsPermissions.ViewTeamTickets,
        TicketsPermissions.ViewAssignedTickets,
        TicketsPermissions.EditTickets,
        TicketsPermissions.ReplyToTickets,
        TicketsPermissions.AddTicketNotes,
        TicketsPermissions.AssignTickets,
        TicketsPermissions.MergeTickets,
        TicketsPermissions.DeleteTickets,
        TicketsPermissions.ViewProtectedTicketInformation,
        TicketsPermissions.ViewTicketReports,
    ];

    private readonly ICatalog<TicketDesk> _desks;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsPermissionsProvider"/> class.
    /// </summary>
    /// <param name="desks">The desks.</param>
    public TicketsPermissionsProvider(ICatalog<TicketDesk> desks)
    {
        _desks = desks;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_global);

        foreach (var desk in (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.OrdinalIgnoreCase))
        {
            permissions.AddRange(TicketsPermissions.DeskOperations.Select(operation => TicketsPermissions.ForDesk(operation, desk)));
        }

        return permissions;
    }

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _global,
        },
    ];
}
