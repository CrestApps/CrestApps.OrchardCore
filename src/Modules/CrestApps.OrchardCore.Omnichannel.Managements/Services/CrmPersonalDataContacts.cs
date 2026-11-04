using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Finds the CRM contacts and leads of the person a privacy request is about, by their primary email and by any of their
/// phone numbers.
/// </summary>
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

        var phoneNumbers = NormalizePhoneNumbers(subject);

        if (phoneNumbers.Length > 0)
        {
            foreach (var contact in await _session.Query<ContentItem, OmnichannelContactPhoneNumberIndex>(index => index.Latest && index.NormalizedPhoneNumber.IsIn(phoneNumbers))
                .Take(MaxContacts)
                .ListAsync(cancellationToken))
            {
                found.TryAdd(contact.ContentItemId, contact);
            }
        }

        return [.. found.Values];
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
