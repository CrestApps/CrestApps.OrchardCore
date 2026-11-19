using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// The permission to cancel a ticket's pending approval, which administrators hold.
/// </summary>
internal sealed class TicketApprovalsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions = [TicketsPermissions.CancelTicketApprovals];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_permissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _permissions,
        },
    ];
}
