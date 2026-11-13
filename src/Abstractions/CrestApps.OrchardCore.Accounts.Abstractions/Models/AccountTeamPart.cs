using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// The staff who look after an account: the owner (the account manager or sales rep who answers for it) and the rest of
/// the account team. Both are staff member ids.
/// </summary>
public sealed class AccountTeamPart : ContentPart
{
    /// <summary>
    /// Gets or sets the staff member who owns the account.
    /// </summary>
    public string OwnerStaffId { get; set; }

    /// <summary>
    /// Gets or sets the other staff members who work with the account, such as support or a technician.
    /// </summary>
    public IList<string> TeamStaffIds { get; set; } = [];
}
