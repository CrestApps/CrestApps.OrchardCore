namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Describes the line type of a phone number as reported by a verification provider.
/// </summary>
public enum PhoneNumberLineType
{
    /// <summary>
    /// The line type could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A mobile (cellular) line.
    /// </summary>
    Mobile = 1,

    /// <summary>
    /// A fixed landline.
    /// </summary>
    Landline = 2,

    /// <summary>
    /// A Voice over IP (VoIP) line.
    /// </summary>
    Voip = 3,

    /// <summary>
    /// A toll-free line.
    /// </summary>
    TollFree = 4,

    /// <summary>
    /// A premium-rate line.
    /// </summary>
    Premium = 5,
}
