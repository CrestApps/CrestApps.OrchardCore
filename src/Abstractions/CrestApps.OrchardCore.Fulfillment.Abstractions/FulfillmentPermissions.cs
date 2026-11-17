using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Fulfillment;

/// <summary>
/// The permissions of the Fulfillment Partners module.
/// </summary>
public static class FulfillmentPermissions
{
    /// <summary>
    /// Allows connecting fulfillment partners and changing their credentials.
    /// </summary>
    public static readonly Permission ManageFulfillmentPartners = new("ManageFulfillmentPartners", LocalizationSource.Create("Manage fulfillment partners and their credentials", typeof(FulfillmentPermissions)), isSecurityCritical: true);
}
