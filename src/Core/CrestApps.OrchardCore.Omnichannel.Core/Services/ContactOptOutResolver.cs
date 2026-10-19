using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The default <see cref="IContactOptOutResolver"/>, reading phone numbers through <see cref="IOmnichannelContactPhoneLookup"/>.
/// </summary>
/// <remarks>
/// Phone and text messages share the number they reach, so a request to stop on either is honoured at every record
/// that holds one of the same numbers, whatever type each record saved the number as. Email is decided by the
/// contact's own preference, because the index holds nothing that would tie two records to the same address.
/// </remarks>
public sealed class ContactOptOutResolver : IContactOptOutResolver
{
    // Records that share a number are few; the cap keeps the IN list of the load bounded all the same.
    private const int LoadBatchSize = 50;

    private readonly ISession _session;
    private readonly IOmnichannelContactPhoneLookup _phoneLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactOptOutResolver"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the records sharing a number are loaded from.</param>
    /// <param name="phoneLookup">The lookup that finds the records by phone number.</param>
    public ContactOptOutResolver(ISession session, IOmnichannelContactPhoneLookup phoneLookup)
    {
        _session = session;
        _phoneLookup = phoneLookup;
    }

    /// <inheritdoc/>
    public async Task<bool> HasOptedOutAsync(ContentItem contact, string channel, CancellationToken cancellationToken = default)
    {
        if (contact is null)
        {
            return false;
        }

        if (OmnichannelContactPreferences.HasOptedOut(contact, channel))
        {
            return true;
        }

        if (channel != OmnichannelConstants.Channels.Phone && channel != OmnichannelConstants.Channels.Sms)
        {
            return false;
        }

        if (string.IsNullOrEmpty(contact.ContentItemId))
        {
            return false;
        }

        var contactItemId = contact.ContentItemId;

        // Every version the index holds, not only the published one: a number that is on the record being loaded
        // but not yet published still leads to the same person.
        var numbers = (await _phoneLookup.GetNumbersAsync([contactItemId], OmnichannelContactVersion.Any, cancellationToken))
            .Select(row => row.NormalizedPhoneNumber)
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (numbers.Length == 0)
        {
            return false;
        }

        // Every other published record reachable at one of these numbers. A record that shares a number is taken to
        // be the same person, because the alternative -- calling them on the other number -- is the one mistake a
        // request to stop cannot tolerate.
        var sharingIds = (await _phoneLookup.FindByNumbersAsync(numbers, OmnichannelContactVersion.Published, cancellationToken: cancellationToken))
            .Select(row => row.ContentItemId)
            .Where(id => !string.IsNullOrEmpty(id) && !string.Equals(id, contactItemId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var batch in sharingIds.Chunk(LoadBatchSize))
        {
            var sharing = await _session
                .Query<ContentItem, OmnichannelContactIndex>(index =>
                    index.Published &&
                    index.ContentItemId.IsIn(batch))
                .ListAsync(cancellationToken);

            if (sharing.Any(other => OmnichannelContactPreferences.HasOptedOut(other, channel)))
            {
                return true;
            }
        }

        return false;
    }
}
