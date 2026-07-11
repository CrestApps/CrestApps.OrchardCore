namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Defines how a phone number filter value should be matched against stored phone numbers.
/// </summary>
public enum PhoneNumberMatchType
{
    /// <summary>
    /// Match phone numbers that exactly equal the given value.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// Match phone numbers that start with the given value.
    /// </summary>
    BeginsWith = 1,

    /// <summary>
    /// Match phone numbers that end with the given value.
    /// </summary>
    EndsWith = 2,

    /// <summary>
    /// Match phone numbers that contain the given value.
    /// </summary>
    Contains = 3,
}
