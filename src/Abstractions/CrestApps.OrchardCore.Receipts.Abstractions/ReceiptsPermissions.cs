using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Receipts;

/// <summary>
/// Defines the permissions used by the Receipts feature.
/// </summary>
public static class ReceiptsPermissions
{
    /// <summary>
    /// The permission required to configure the receipt branding settings.
    /// </summary>
    public static readonly Permission ManageReceiptSettings = new("ManageReceiptSettings", "Manage receipt settings");
}
