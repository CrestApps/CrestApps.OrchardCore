using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Finds the CRM contacts and leads of the person a privacy request is about, by their primary email and by any of their
/// phone numbers.
/// </summary>
/// <remarks>
/// A phone number finds a contact only when no other contact has it: a switchboard, a household line or a shared mobile
/// belongs to several people, and erasing all of them by it would erase people the request is not about. Such a number
/// is reported in <see cref="CrmPersonalDataMatch.SharedPhoneNumbers"/>, so the request can warn that those contacts need
/// a look by hand. Converted leads do not count as another person, because a lead and the contact it became are one.
/// </remarks>
internal sealed class CrmPersonalDataContacts
{
    // A privacy request reads at most this many contacts for each way the person is identified.
    private const int MaxContacts = 500;

    private readonly ISession _session;
    private readonly IPhoneNumberService _phoneNumberService;

    public CrmPersonalDataContacts(
        ISession session,
        IPhoneNumberService phoneNumberService)
    {
        _session = session;
        _phoneNumberService = phoneNumberService;
    }

    /// <summary>
    /// Returns the latest version of each of the person's contacts.
    /// </summary>
    public async Task<IReadOnlyList<ContentItem>> FindAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
        => (await MatchAsync(subject, cancellationToken)).Contacts;

    /// <summary>
    /// Returns the latest version of each of the person's contacts, and the person's phone numbers that other contacts
    /// share, which found nobody.
    /// </summary>
    public async Task<CrmPersonalDataMatch> MatchAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var found = new Dictionary<string, ContentItem>(StringComparer.Ordinal);

        // The primary email is indexed as it was entered, so the forms the person gave and their lowercase are both tried.
        var emails = subject.Emails
            .SelectMany(email => new[] { email.Trim(), email.Trim().ToLowerInvariant() })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (emails.Length > 0)
        {
            foreach (var contact in await _session.Query<ContentItem, OmnichannelContactIndex>(index => index.Latest && index.PrimaryEmailAddress.IsIn(emails))
                .Take(MaxContacts)
                .ListAsync(cancellationToken))
            {
                found.TryAdd(contact.ContentItemId, contact);
            }
        }

        var sharedPhoneNumbers = new List<string>();
        var phoneNumbers = NormalizePhoneNumbers(subject);

        if (phoneNumbers.Length > 0)
        {
            var rows = await _session.QueryIndex<OmnichannelContactPhoneNumberIndex>(index => index.Latest && index.NormalizedPhoneNumber.IsIn(phoneNumbers))
                .Take(MaxContacts)
                .ListAsync(cancellationToken);

            var contactIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var number in rows.GroupBy(row => row.NormalizedPhoneNumber, StringComparer.Ordinal))
            {
                var people = number
                    .Where(row => !row.IsConverted)
                    .Select(row => row.ContentItemId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (people.Length > 1)
                {
                    sharedPhoneNumbers.Add(number.Key);

                    continue;
                }

                foreach (var row in number)
                {
                    contactIds.Add(row.ContentItemId);
                }
            }

            contactIds.ExceptWith(found.Keys);

            foreach (var batch in contactIds.Chunk(MaxContacts))
            {
                foreach (var contact in await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentItemId.IsIn(batch))
                    .ListAsync(cancellationToken))
                {
                    found.TryAdd(contact.ContentItemId, contact);
                }
            }
        }

        return new CrmPersonalDataMatch([.. found.Values], sharedPhoneNumbers);
    }

    /// <summary>
    /// Returns the person's phone numbers in E.164 form, as contact phone numbers are indexed.
    /// </summary>
    public string[] NormalizePhoneNumbers(PersonalDataSubject subject)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var phoneNumber in subject.PhoneNumbers)
        {
            if (_phoneNumberService.TryParse(phoneNumber, out var canonical))
            {
                normalized.Add(canonical.Value);
            }
        }

        return [.. normalized];
    }
}
