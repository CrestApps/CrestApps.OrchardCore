using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
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
/// An opt-out recorded against an address that no contact held (a STOP from an unknown number) is honoured for every
/// record that later holds that number.
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

        // A request to stop sent from one of these numbers while no record held it was recorded against the number.
        if (await FindAddressOptOutsAsync(channel, numbers, cancellationToken) is { Count: > 0 })
        {
            return true;
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

    /// <inheritdoc/>
    public async Task<bool> HasAddressOptedOutAsync(string channel, string address, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAddress(channel, address);

        if (string.IsNullOrEmpty(channel) || normalized is null)
        {
            return false;
        }

        return await FindAddressOptOutsAsync(channel, [normalized], cancellationToken) is { Count: > 0 };
    }

    /// <inheritdoc/>
    public async Task RecordAddressOptOutAsync(ContactAddressOptOut optOut, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(optOut);
        ArgumentException.ThrowIfNullOrEmpty(optOut.Channel);

        var normalized = NormalizeAddress(optOut.Channel, optOut.Address);

        if (normalized is null)
        {
            return;
        }

        var existing = await FindAddressOptOutsAsync(optOut.Channel, [normalized], cancellationToken);

        if (existing.Count > 0)
        {
            return;
        }

        optOut.Address = normalized;

        if (string.IsNullOrEmpty(optOut.ItemId))
        {
            optOut.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(optOut, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ClearAddressOptOutAsync(string channel, string address, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAddress(channel, address);

        if (string.IsNullOrEmpty(channel) || normalized is null)
        {
            return false;
        }

        // Two opt-outs recorded at the same moment can each have created a record; opting back in clears them all.
        var records = await FindAddressOptOutsAsync(channel, [normalized], cancellationToken);

        foreach (var record in records)
        {
            _session.Delete(record, OmnichannelConstants.CollectionName);
        }

        return records.Count > 0;
    }

    private async Task<IReadOnlyList<ContactAddressOptOut>> FindAddressOptOutsAsync(string channel, string[] addresses, CancellationToken cancellationToken)
    {
        var optOuts = new List<ContactAddressOptOut>();

        foreach (var batch in addresses.Chunk(LoadBatchSize))
        {
            optOuts.AddRange(await _session
                .Query<ContactAddressOptOut, ContactAddressOptOutIndex>(
                    index => index.Channel == channel && index.Address.IsIn(batch),
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync(cancellationToken));
        }

        return optOuts;
    }

    // Phone numbers are compared in E.164 form, the form the contact index and the conversations hold them in.
    private static string NormalizeAddress(string channel, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var trimmed = address.Trim();

        if (channel != OmnichannelConstants.Channels.Phone && channel != OmnichannelConstants.Channels.Sms)
        {
            return trimmed.ToLowerInvariant();
        }

        return PhoneNumber.TryFromE164(trimmed, out var phoneNumber) && phoneNumber.HasValue
            ? phoneNumber.Value
            : trimmed;
    }
}
