using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

/// <summary>
/// Writes one <see cref="OmnichannelContactPhoneNumberIndex"/> row for each distinct phone number on a contact or
/// lead, so a lookup by number finds the record whatever type the number is saved as.
/// </summary>
internal sealed class OmnichannelContactPhoneNumberIndexProvider : IndexProvider<ContentItem>
{
    private readonly IPhoneNumberService _phoneNumberService;

    public OmnichannelContactPhoneNumberIndexProvider(IPhoneNumberService phoneNumberService)
    {
        _phoneNumberService = phoneNumberService;
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
            !contact.TryGet<OmnichannelContactPart>(out _) ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is null ||
            bagPart.ContentItems.Count == 0)
        {
            return [];
        }

        var contentType = Truncate(contact.ContentType, 255);
        var isConverted = contact.TryGet<LeadPart>(out var leadPart) && leadPart.IsConverted;
        var indexes = new List<OmnichannelContactPhoneNumberIndex>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contactMethod in bagPart.ContentItems)
        {
            if (!string.Equals(contactMethod.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) ||
                !contactMethod.TryGet<PhoneNumberInfoPart>(out var phonePart) ||
                string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                continue;
            }

            var normalized = OmnichannelContactIndexProvider.NormalizeToE164(_phoneNumberService, phonePart.Number);

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
                PhoneType = Truncate(phonePart.Type?.Text?.Trim(), 50),
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
