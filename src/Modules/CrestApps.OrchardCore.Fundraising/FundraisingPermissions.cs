using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Fundraising;

public static class FundraisingPermissions
{
    /// <summary>
    /// Sets who receipts are from, and sees donations.
    /// </summary>
    public static readonly Permission ManageFundraising = new(
        "ManageFundraising",
        LocalizationSource.Create("Manage fundraising", typeof(FundraisingPermissions)));
}
