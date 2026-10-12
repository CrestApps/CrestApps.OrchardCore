namespace CrestApps.OrchardCore.PayLater;

/// <summary>
/// Provides shared constant values for the Pay Later module.
/// </summary>
public static class PayLaterConstants
{
    /// <summary>
    /// The identifier of the site settings group used to configure Pay Later.
    /// </summary>
    public const string SettingsGroupId = "paylater";

    /// <summary>
    /// Contains the feature identifiers exposed by the Pay Later module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Pay Later feature that contributes an offline pay-later option to the checkout framework.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.PayLater";
    }
}
