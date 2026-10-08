using OrchardCore.Localization;
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
    public static readonly Permission ManageTelephonySettings = new("ManageTelephonySettings", LocalizationSource.Create("Manage telephony settings", typeof(TelephonyPermissions)));

    /// <summary>
    /// The permission required to use the soft phone to place and control calls.
    /// </summary>
    public static readonly Permission UseSoftPhone = new("UseTelephonySoftPhone", LocalizationSource.Create("Use the telephony soft phone", typeof(TelephonyPermissions)));

    /// <summary>
    /// The permission required to manage internal extensions (the number-to-user registry).
    /// </summary>
    public static readonly Permission ManageExtensions = new("ManageTelephonyExtensions", LocalizationSource.Create("Manage telephony extensions", typeof(TelephonyPermissions)));
}
