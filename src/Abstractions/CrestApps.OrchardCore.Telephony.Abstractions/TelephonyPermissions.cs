using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Defines the permissions used by the Telephony feature and its providers.
/// </summary>
public static class TelephonyPermissions
{
    /// <summary>
    /// The permission required to configure telephony and provider settings.
    /// </summary>
    public static readonly Permission ManageTelephonySettings = new("ManageTelephonySettings", "Manage telephony settings");

    /// <summary>
    /// The permission required to use the soft phone to place and control calls.
    /// </summary>
    public static readonly Permission UseSoftPhone = new("UseTelephonySoftPhone", "Use the telephony soft phone");
}
