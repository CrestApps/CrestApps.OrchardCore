using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;
using YesSql.Indexes;
using static CrestApps.OrchardCore.StringTruncation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

/// <summary>
/// Writes one <see cref="OmnichannelContactEmailAddressIndex"/> row for each distinct, valid email address on a
/// contact or lead, so a lookup by address finds the record whichever of its addresses was used.
/// </summary>
/// <remarks>
/// It writes a row per address, so it does not declare a single row per document: a contact with two addresses would
/// make the save throw.
/// </remarks>
internal sealed class OmnichannelContactEmailAddressIndexProvider : IndexProvider<ContentItem>
{
    private readonly IContactMethodsAccessor _contactMethods;

    public OmnichannelContactEmailAddressIndexProvider(IContactMethodsAccessor contactMethods)
    {
        _contactMethods = contactMethods;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OmnichannelContactEmailAddressIndex>()
            .ExclusiveToDocumentType()
            .Map(CreateIndexes);
    }

    internal IEnumerable<OmnichannelContactEmailAddressIndex> CreateIndexes(ContentItem contact)
    {
        if ((!contact.Published && !contact.Latest) ||
            !contact.TryGet<OmnichannelContactPart>(out _))
        {
            return [];
        }

        var emailAddresses = _contactMethods.Get(contact).EmailAddresses;

        if (emailAddresses.Count == 0)
        {
            return [];
        }

        var contentType = TruncateOrNull(contact.ContentType, 255);
        var isConverted = contact.TryGet<LeadPart>(out var leadPart) && leadPart.IsConverted;
        var indexes = new List<OmnichannelContactEmailAddressIndex>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var emailAddress in emailAddresses)
        {
            // An address that cannot receive mail finds nobody; one saved twice, in two spellings, is one row.
            if (!OmnichannelEmailAddress.IsValid(emailAddress?.Email))
            {
                continue;
            }

            var normalized = OmnichannelEmailAddress.Normalize(emailAddress.Email);

            if (!seen.Add(normalized))
            {
                continue;
            }

            indexes.Add(new OmnichannelContactEmailAddressIndex
            {
                ContentItemId = contact.ContentItemId,
                ContentType = contentType,
                IsConverted = isConverted,
                Published = contact.Published,
                Latest = contact.Latest,
                NormalizedEmailAddress = TruncateOrNull(normalized, OmnichannelContactEmailAddressIndex.EmailAddressLength),
            });
        }

        return indexes;
    }
}
