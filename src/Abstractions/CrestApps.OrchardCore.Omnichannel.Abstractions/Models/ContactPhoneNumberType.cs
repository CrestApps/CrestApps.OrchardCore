namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// The well-known kinds of phone number a contact can carry.
/// </summary>
/// <remarks>
/// Deliberately strings rather than an enumeration: the values are stored as free text today, and a
/// tenant may have typed something that is none of these. A caller matches on these constants and
/// treats anything else as an unclassified number rather than refusing to load the contact.
/// </remarks>
public static class ContactPhoneNumberType
{
    /// <summary>
    /// A mobile number, which is the one an SMS conversation uses.
    /// </summary>
    public const string Cell = "Cell";

    /// <summary>
    /// A home number.
    /// </summary>
    public const string Home = "Home";

    /// <summary>
    /// A work number.
    /// </summary>
    public const string Work = "Work";
}
