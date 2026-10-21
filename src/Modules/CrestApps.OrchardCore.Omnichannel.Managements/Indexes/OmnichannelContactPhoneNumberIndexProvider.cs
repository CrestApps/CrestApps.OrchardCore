using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

/// <summary>
/// Writes one <see cref="OmnichannelContactPhoneNumberIndex"/> row for each distinct phone number on a contact or
/// lead, so a lookup by number finds the record whatever type the number is saved as.
/// </summary>
internal sealed class OmnichannelContactPhoneNumberIndexProvider : IndexProvider<ContentItem>
{
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IContactMethodsAccessor _contactMethods;

    public OmnichannelContactPhoneNumberIndexProvider(
        IPhoneNumberService phoneNumberService,
        IContactMethodsAccessor contactMethods)
    {
        _phoneNumberService = phoneNumberService;
        _contactMethods = contactMethods;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OmnichannelContactPhoneNumberIndex>()
            .Map(CreateIndexes);
    }

    internal IEnumerable<OmnichannelContactPhoneNumberIndex> CreateIndexes(ContentItem contact)
    {
        if ((!contact.Published && !contact.Latest) ||
            !contact.TryGet<OmnichannelContactPart>(out _))
        {
            return [];
        }

        var phoneNumbers = _contactMethods.Get(contact).PhoneNumbers;

        if (phoneNumbers.Count == 0)
        {
            return [];
        }

        var contentType = Truncate(contact.ContentType, 255);
        var isConverted = contact.TryGet<LeadPart>(out var leadPart) && leadPart.IsConverted;
        var indexes = new List<OmnichannelContactPhoneNumberIndex>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var phoneNumber in phoneNumbers)
        {
            var normalized = OmnichannelContactIndexProvider.NormalizeToE164(_phoneNumberService, phoneNumber);

            // A number saved twice, under one type or two, is one way to reach the record; the first type it was
            // saved under is the one kept.
            if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            indexes.Add(new OmnichannelContactPhoneNumberIndex
            {
                ContentItemId = contact.ContentItemId,
                ContentType = contentType,
                IsConverted = isConverted,
                Published = contact.Published,
                Latest = contact.Latest,
                PhoneType = Truncate(phoneNumber.Type, 50),
                NormalizedPhoneNumber = normalized,
            });
        }

        return indexes;
    }

    private static string Truncate(string value, int maxLength)
    {
        return string.IsNullOrEmpty(value)
            ? null
            : value.Substring(0, Math.Min(maxLength, value.Length));
    }
}
