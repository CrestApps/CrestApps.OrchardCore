using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;

/// <summary>
/// Permissions for the Phone Number Verifications module.
/// </summary>
public static class PhoneNumberVerificationsPermissions
{
    /// <summary>
    /// Gets the permission to manage phone number verification settings.
    /// </summary>
    public static readonly Permission ManagePhoneNumberVerificationSettings = new(
        "ManagePhoneNumberVerificationSettings",
        LocalizationSource.Create("Manage phone number verification settings", typeof(PhoneNumberVerificationsPermissions)));

    /// <summary>
    /// Gets the permission to verify phone numbers.
    /// </summary>
    public static readonly Permission VerifyPhoneNumbers = new(
        "VerifyPhoneNumbers",
        LocalizationSource.Create("Verify phone numbers", typeof(PhoneNumberVerificationsPermissions)));

    /// <summary>
    /// Gets the permission to run the Phone Number Verifications report.
    /// </summary>
    public static readonly Permission RunPhoneNumberVerificationsReport = new(
        "RunPhoneNumberVerificationsReport",
        LocalizationSource.Create("Run 'Phone Number Verifications' Report", typeof(PhoneNumberVerificationsPermissions)));
}
