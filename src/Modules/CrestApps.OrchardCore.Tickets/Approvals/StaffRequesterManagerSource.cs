using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tickets.Services;

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// A requester's manager is the user of the manager on their staff record.
/// </summary>
internal sealed class StaffRequesterManagerSource : ITicketRequesterManagerSource
{
    private readonly IStaffDirectory _staff;

    public StaffRequesterManagerSource(IStaffDirectory staff)
    {
        _staff = staff;
    }

    public async Task<string> FindManagerUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)
            || await _staff.FindByUserIdAsync(userId, cancellationToken) is not { ManagerId: { Length: > 0 } managerId })
        {
            return null;
        }

        var managers = await _staff.GetAsync([managerId], cancellationToken);

        return managers.TryGetValue(managerId, out var manager) && !string.IsNullOrEmpty(manager.UserId) ? manager.UserId : null;
    }
}
