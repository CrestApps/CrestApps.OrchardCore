namespace CrestApps.OrchardCore.Asterisk;

/// <summary>
/// The identifiers this host knows the Asterisk features by.
/// </summary>
/// <remarks>
/// <para>
/// These stay with the host for the same reason the telephony ones do: a feature is the host's notion, and a
/// host has already written this exact string into its database. Renaming it does not rename a feature, it
/// makes the feature a tenant had enabled disappear. Everything else that was on the constants class
/// travelled with the code into <c>CrestApps.Core.Telephony.Asterisk</c>.
/// </para>
/// <para>
/// Named for what it holds rather than kept as a nested class on a second <c>AsteriskConstants</c>, so that a
/// file importing both this and the extracted package has nothing ambiguous to resolve.
/// </para>
/// </remarks>
public static class AsteriskFeatures
{
    /// <summary>
    /// The identifier of the Asterisk provider feature.
    /// </summary>
    public const string Area = "CrestApps.OrchardCore.Asterisk";
}
