using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The default <see cref="IContactOptOutResolver"/>, reading phone numbers from the contact index.
/// </summary>
/// <remarks>
/// Phone and text messages share the number they reach, so a request to stop on either is honoured at every record
/// that holds one of the same numbers. Email is decided by the contact's own preference, because the index holds
/// nothing that would tie two records to the same address.
/// </remarks>
public sealed class ContactOptOutResolver : IContactOptOutResolver
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactOptOutResolver"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query the contact index.</param>
    public ContactOptOutResolver(ISession session)
    {
        _session = session;
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

        var numbers = await GetPhoneNumbersAsync(contact.ContentItemId, cancellationToken);

        if (numbers.Length == 0)
        {
            return false;
        }

        var contactItemId = contact.ContentItemId;

        // Every other published record reachable at one of these numbers. A record that shares a number is taken to
        // be the same person, because the alternative -- calling them on the other number -- is the one mistake a
        // request to stop cannot tolerate.
        var sharing = await _session
            .Query<ContentItem, OmnichannelContactIndex>(index =>
                index.Published &&
                index.ContentItemId != contactItemId &&
                (index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers) || index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers)))
            .ListAsync(cancellationToken);

        return sharing.Any(other => OmnichannelContactPreferences.HasOptedOut(other, channel));
    }

    private async Task<string[]> GetPhoneNumbersAsync(string contactItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contactItemId))
        {
            return [];
        }

        // Every version the index holds, not only the published one: a number that is on the record being loaded
        // but not yet published still leads to the same person.
        var rows = await _session
            .QueryIndex<OmnichannelContactIndex>(index => index.ContentItemId == contactItemId)
            .ListAsync(cancellationToken);

        return rows
            .SelectMany(row => new[] { row.NormalizedPrimaryCellPhoneNumber, row.NormalizedPrimaryHomePhoneNumber })
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
