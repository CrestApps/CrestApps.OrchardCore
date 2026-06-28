namespace CrestApps.OrchardCore.PhoneNumbers.Core;

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
        /// The feature identifier for the Phone Numbers module.
        /// </summary>
        public const string PhoneNumbers = "CrestApps.OrchardCore.PhoneNumbers";

        /// <summary>
        /// The core Phone Number Verifications feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.PhoneNumbers.Verifications";

        /// <summary>
        /// The AbstractAPI phone number verification provider feature.
        /// </summary>
        public const string AbstractApi = "CrestApps.OrchardCore.PhoneNumbers.Verifications.AbstractApi";

        /// <summary>
        /// The Veriphone phone number verification provider feature.
        /// </summary>
        public const string Veriphone = "CrestApps.OrchardCore.PhoneNumbers.Verifications.Veriphone";

        /// <summary>
        /// The Twilio phone number verification provider feature.
        /// </summary>
        public const string Twilio = "CrestApps.OrchardCore.PhoneNumbers.Verifications.Twilio";
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

        /// <summary>
        /// The Twilio provider key.
        /// </summary>
        public const string Twilio = "Twilio";
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
