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

    /// <summary>
    /// Returns the normalized phone numbers of the stored records the predicate accepts, with the records that own
    /// each number. An import uses it to compare against only the records its duplicate scope covers, such as
    /// contacts but not leads.
    /// </summary>
    /// <param name="include">Decides, from the record's contact index row, whether its numbers count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A mapping of normalized phone numbers to owning content item identifiers.</returns>
    async Task<Dictionary<string, string[]>> GetExistingNormalizedPhoneNumberOwnersAsync(
        Func<CrestApps.OrchardCore.Omnichannel.Core.Indexes.OmnichannelContactIndex, bool> include,
        CancellationToken cancellationToken)
        => await GetAllExistingNormalizedPhoneNumberOwnersAsync(cancellationToken);
}
