using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PhoneNumberVerifications;

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
    /// Gets the permission to verify phone numbers and view verification reports.
    /// </summary>
    public static readonly Permission VerifyPhoneNumbers = new(
        "VerifyPhoneNumbers",
        "Verify phone numbers and view verification reports");
}
