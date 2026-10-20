using CrestApps.OrchardCore.Omnichannel.Core.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Looks up existing omnichannel contact phone numbers for duplicate detection during import. Every lookup reads only
/// the records holding one of the numbers asked about; there is deliberately no way to read every stored number, which
/// on a tenant with hundreds of thousands of records ran an import out of memory.
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
    /// Returns, for each of <paramref name="phoneNumberKeys"/> that a stored record the predicate accepts holds, the
    /// records that hold it. Only the records holding one of the keys are read, a bounded number of keys per query, so
    /// an import checks a batch of rows without reading every stored record into memory.
    /// </summary>
    /// <param name="phoneNumberKeys">The comparison keys of the numbers to look for.</param>
    /// <param name="include">Decides, from the index row that matched the record, whether its numbers count, or
    /// <see langword="null"/> to count every record.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A mapping of the matching keys to owning content item identifiers.</returns>
    Task<Dictionary<string, string[]>> GetPhoneNumberOwnersAsync(
        IReadOnlyCollection<string> phoneNumberKeys,
        Func<IOmnichannelContactMatch, bool> include,
        CancellationToken cancellationToken);
}
