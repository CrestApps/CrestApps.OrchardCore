namespace CrestApps.OrchardCore.Receipts.Core;

/// <summary>
/// Contains constant values used by the Receipts feature.
/// </summary>
public static class ReceiptsConstants
{
    /// <summary>
    /// The identifier of the site settings group used to configure receipt branding.
    /// </summary>
    public const string SettingsGroupId = "receipts";

    /// <summary>
    /// Contains the feature identifiers exposed by the Receipts module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the main Receipts feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Receipts";
    }
}
