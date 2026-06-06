namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Provides phone number parsing, validation, and formatting operations
/// using the E.164 international standard.
/// </summary>
public interface IPhoneNumberService
{
    /// <summary>
    /// Attempts to parse a phone number and format it as E.164.
    /// </summary>
    /// <param name="phoneNumber">The raw phone number input.</param>
    /// <param name="regionCode">
    /// The ISO 3166-1 alpha-2 region code (e.g., "US", "GB") used as context
    /// when the phone number does not include a country calling code.
    /// Can be <see langword="null"/> if the number already contains a leading <c>+</c>.
    /// </param>
    /// <param name="e164Number">
    /// When this method returns <see langword="true"/>, contains the phone number
    /// in E.164 format (e.g., <c>+17024993350</c>).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the phone number was successfully parsed and is valid;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool TryFormatToE164(string phoneNumber, string regionCode, out string e164Number);

    /// <summary>
    /// Validates whether the given phone number is valid for the specified region.
    /// </summary>
    /// <param name="phoneNumber">The phone number to validate.</param>
    /// <param name="regionCode">
    /// The ISO 3166-1 alpha-2 region code. Can be <see langword="null"/>
    /// if the number already includes an international prefix.
    /// </param>
    /// <returns><see langword="true"/> if the number is valid; otherwise, <see langword="false"/>.</returns>
    bool IsValidNumber(string phoneNumber, string regionCode);

    /// <summary>
    /// Gets the IANA time zone identifiers associated with an E.164 phone number.
    /// </summary>
    /// <param name="e164Number">The phone number in E.164 format.</param>
    /// <returns>A list of IANA time zone identifiers, or an empty list if unavailable.</returns>
    IReadOnlyList<string> GetTimeZones(string e164Number);

    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 region code for an E.164 phone number.
    /// </summary>
    /// <param name="e164Number">The phone number in E.164 format.</param>
    /// <returns>The region code (e.g., "US"), or <see langword="null"/> if it cannot be determined.</returns>
    string GetRegionCode(string e164Number);

    /// <summary>
    /// Gets the international country calling code for a given region.
    /// </summary>
    /// <param name="regionCode">The ISO 3166-1 alpha-2 region code (e.g., "US").</param>
    /// <returns>The country calling code (e.g., 1 for US), or 0 if the region is unknown.</returns>
    int GetCountryCode(string regionCode);

    /// <summary>
    /// Gets all supported ISO 3166-1 alpha-2 region codes.
    /// </summary>
    /// <returns>A collection of supported region codes.</returns>
    IReadOnlyCollection<string> GetSupportedRegions();
}
