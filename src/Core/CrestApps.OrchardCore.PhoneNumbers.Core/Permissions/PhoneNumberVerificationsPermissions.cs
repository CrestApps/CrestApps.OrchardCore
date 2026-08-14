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
        "Manage phone number verification settings");

    /// <summary>
    /// Gets the permission to verify phone numbers.
    /// </summary>
    public static readonly Permission VerifyPhoneNumbers = new(
        "VerifyPhoneNumbers",
        "Verify phone numbers");

    /// <summary>
    /// Gets the permission to run the Phone Number Verifications report.
    /// </summary>
    public static readonly Permission RunPhoneNumberVerificationsReport = new(
        "RunPhoneNumberVerificationsReport",
        "Run 'Phone Number Verifications' Report");
}
