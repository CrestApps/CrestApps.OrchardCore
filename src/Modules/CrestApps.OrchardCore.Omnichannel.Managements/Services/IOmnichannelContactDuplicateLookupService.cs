namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Looks up existing omnichannel contact phone numbers for duplicate detection during import.
/// </summary>
public interface IOmnichannelContactDuplicateLookupService
{
    /// <summary>
    /// Returns the subset of <paramref name="phoneNumbers"/> that already exist for stored omnichannel contacts.
    /// </summary>
    /// <param name="phoneNumbers">The normalized phone numbers to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized phone numbers that already exist.</returns>
    Task<HashSet<string>> GetExistingNormalizedPhoneNumbersAsync(
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns all normalized phone numbers currently stored across all omnichannel contacts.
    /// Used to pre-load the full set for duplicate detection during import.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A set of all existing normalized phone numbers.</returns>
    Task<HashSet<string>> GetAllExistingNormalizedPhoneNumbersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns all normalized phone numbers currently stored across all omnichannel contacts,
    /// along with the content item identifiers that own each number.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A mapping of normalized phone numbers to owning content item identifiers.</returns>
    Task<Dictionary<string, string[]>> GetAllExistingNormalizedPhoneNumberOwnersAsync(CancellationToken cancellationToken);
}
