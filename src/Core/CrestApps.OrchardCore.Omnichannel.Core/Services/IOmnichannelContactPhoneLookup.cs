using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Finds contacts and leads by phone number, and the phone numbers of a contact or lead, across every number the
/// record has, whatever type each one is saved as.
/// </summary>
/// <remarks>
/// Each row returned is one number of one record. A record matched by two numbers, or by the same number on its
/// published and latest versions, comes back once per number, so callers that need records take the distinct
/// <see cref="OmnichannelContactPhoneNumberIndex.ContentItemId"/> values.
/// </remarks>
public interface IOmnichannelContactPhoneLookup
{
    /// <summary>
    /// Finds the records reachable at any of the given numbers.
    /// </summary>
    /// <param name="normalizedPhoneNumbers">The numbers to look for, in E.164 format.</param>
    /// <param name="version">Which indexed versions to read.</param>
    /// <param name="contentTypes">The content types to keep, or <see langword="null"/> to keep every type.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>One row per matching number of each matching record.</returns>
    Task<IReadOnlyList<OmnichannelContactPhoneNumberIndex>> FindByNumbersAsync(
        IReadOnlyCollection<string> normalizedPhoneNumbers,
        OmnichannelContactVersion version,
        IReadOnlyCollection<string> contentTypes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the numbers of the given records.
    /// </summary>
    /// <param name="contentItemIds">The content item ids of the records.</param>
    /// <param name="version">Which indexed versions to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>One row per number of each record.</returns>
    Task<IReadOnlyList<OmnichannelContactPhoneNumberIndex>> GetNumbersAsync(
        IReadOnlyCollection<string> contentItemIds,
        OmnichannelContactVersion version,
        CancellationToken cancellationToken = default);
}
