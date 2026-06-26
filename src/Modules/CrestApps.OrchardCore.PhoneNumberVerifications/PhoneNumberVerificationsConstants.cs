namespace CrestApps.OrchardCore.PhoneNumberVerifications;

/// <summary>
/// Contains constant values for the Phone Number Verifications module.
/// </summary>
public static class PhoneNumberVerificationsConstants
{
    /// <summary>
    /// The name of the content part that stores phone number verification data on a content item.
    /// </summary>
    public const string VerificationPartName = "PhoneNumberVerificationPart";

    /// <summary>
    /// Represents the feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The core Phone Number Verifications feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.PhoneNumberVerifications";

        /// <summary>
        /// The AbstractAPI phone number verification provider feature.
        /// </summary>
        public const string AbstractApi = "CrestApps.OrchardCore.PhoneNumberVerifications.AbstractApi";

        /// <summary>
        /// The Veriphone phone number verification provider feature.
        /// </summary>
        public const string Veriphone = "CrestApps.OrchardCore.PhoneNumberVerifications.Veriphone";
    }

    /// <summary>
    /// Represents the well-known provider keys shipped with the module.
    /// </summary>
    public static class Providers
    {
        /// <summary>
        /// The AbstractAPI provider key.
        /// </summary>
        public const string AbstractApi = "AbstractApi";

        /// <summary>
        /// The Veriphone provider key.
        /// </summary>
        public const string Veriphone = "Veriphone";
    }

    /// <summary>
    /// Represents the settings group identifiers used by the admin settings pages.
    /// </summary>
    public static class SettingsGroupIds
    {
        /// <summary>
        /// The settings group that hosts the core settings and every provider settings tab.
        /// </summary>
        public const string Verifications = "phoneNumberVerifications";
    }
}
