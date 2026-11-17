using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Staff.Services;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// Tells that a signed-in user is the staff member linked to their account, so self-service pages show them the records
/// their staff record owns.
/// </summary>
internal sealed class StaffUserPartySource : IUserPartySource
{
    private readonly IStaffMemberManager _staffManager;

    public StaffUserPartySource(IStaffMemberManager staffManager)
    {
        _staffManager = staffManager;
    }

    public async Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var staff = await _staffManager.FindByUserIdAsync(userId, cancellationToken);

        return staff is null ? [] : [new PartyRef(StaffConstants.PartyKind, staff.ItemId)];
    }
}
