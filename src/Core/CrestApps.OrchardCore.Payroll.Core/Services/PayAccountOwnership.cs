using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Services;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Whose bank account net pay may go to: only the person's own, owned by their staff member or by their user. Checked
/// when a pay profile is saved, when a run is calculated and again when a deposit is made.
/// </summary>
public static class PayAccountOwnership
{
    /// <summary>
    /// Gets whether an account belongs to a staff member, directly or through their user.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="userId">The staff member's user, or <see langword="null"/>.</param>
    /// <returns>Whether the account is theirs.</returns>
    public static bool IsOwnedBy(BankAccount account, string staffMemberId, string userId)
        => account is not null
            && !account.Owner.IsEmpty
            && !string.IsNullOrEmpty(account.Owner.Id)
            && ((account.Owner.Kind == StaffConstants.PartyKind && account.Owner.Id == staffMemberId)
                || (!string.IsNullOrEmpty(userId) && account.Owner.Kind == PartyKinds.User && account.Owner.Id == userId));

    /// <summary>
    /// Gets whether an account belongs to a staff member, directly or through their user.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="staff">The staff directory, for the member's user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the account is theirs.</returns>
    public static async Task<bool> IsOwnedByAsync(BankAccount account, string staffMemberId, IStaffDirectory staff, CancellationToken cancellationToken = default)
    {
        if (account is null || string.IsNullOrEmpty(staffMemberId))
        {
            return false;
        }

        if (account.Owner.Kind == StaffConstants.PartyKind)
        {
            return IsOwnedBy(account, staffMemberId, null);
        }

        var userId = staff is null || account.Owner.Kind != PartyKinds.User
            ? null
            : (await staff.GetAsync([staffMemberId], cancellationToken)).GetValueOrDefault(staffMemberId)?.UserId;

        return IsOwnedBy(account, staffMemberId, userId);
    }
}
