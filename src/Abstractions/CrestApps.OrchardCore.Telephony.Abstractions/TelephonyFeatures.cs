namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// The identifiers this host knows the telephony features by.
/// </summary>
/// <remarks>
/// <para>
/// These stay with the host because a feature is the host's notion, and because a host has already written
/// these exact strings into its database: renaming one does not rename a feature, it makes the feature a
/// tenant had enabled disappear. Everything else that was on the constants class travels with the code.
/// </para>
/// <para>
/// Named for what it holds rather than kept as a nested class on a second <c>TelephonyConstants</c>. Two
/// classes of that name, one here and one in the extracted package, would be ambiguous in every file that
/// imports both - which is most of them.
/// </para>
/// </remarks>
public static class TelephonyFeatures
{
    /// <summary>
    /// The identifier of the core Telephony feature.
    /// </summary>
    public const string Area = "CrestApps.OrchardCore.Telephony";

    /// <summary>
    /// The identifier of the shared soft-phone feature the user-facing soft phones depend on.
    /// </summary>
    public const string SoftPhoneCore = "CrestApps.OrchardCore.Telephony.SoftPhone.Core";

    /// <summary>
    /// The identifier of the soft-phone widget feature.
    /// </summary>
    public const string SoftPhone = "CrestApps.OrchardCore.Telephony.SoftPhone";

    /// <summary>
    /// The identifier of the soft-phone extension feature.
    /// </summary>
    public const string SoftPhoneExtension = "CrestApps.OrchardCore.Telephony.SoftPhone.Extension";
}
