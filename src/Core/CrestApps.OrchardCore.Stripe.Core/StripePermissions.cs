using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Defines permissions used by the Stripe module.
/// </summary>
public static class StripePermissions
{
    /// <summary>
    /// Allows users to manage Stripe settings in the admin area.
    /// </summary>
    public static readonly Permission ManageStripeSettings = new(
        "ManageStripeSettings",
        LocalizationSource.Create("Manage Stripe Settings", typeof(StripePermissions)),
        isSecurityCritical: true);
}
