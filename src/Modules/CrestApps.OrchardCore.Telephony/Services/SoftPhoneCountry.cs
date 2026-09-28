namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Represents a selectable country used by the soft phone's phone number input.
/// </summary>
/// <param name="Code">The ISO 3166-1 alpha-2 country code, in lower case.</param>
/// <param name="Name">The English display name of the country.</param>
internal readonly record struct SoftPhoneCountry(string Code, string Name);
