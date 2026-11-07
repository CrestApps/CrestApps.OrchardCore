using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;
using static CrestApps.OrchardCore.StringTruncation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class LeadIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<LeadIndex>()
            .Map(CreateIndex);
    }

    internal static LeadIndex CreateIndex(ContentItem contentItem)
    {
        if ((!contentItem.Published && !contentItem.Latest) ||
            !contentItem.TryGet<LeadPart>(out var part))
        {
            return null;
        }

        return new LeadIndex
        {
            ContentItemId = contentItem.ContentItemId,
            ContentType = TruncateOrNull(contentItem.ContentType, 255),
            Published = contentItem.Published,
            Latest = contentItem.Latest,
            StatusId = TruncateOrNull(part.StatusId, 50),
            IsClosed = part.IsClosed || part.IsConverted,
            IsConverted = part.IsConverted,
            SourceId = TruncateOrNull(part.Source.GetFirstContentItemId(), 26),
            ListName = TruncateOrNull(part.ListName.GetTrimmedText(), 255),
            Rating = TruncateOrNull(part.Rating.GetTrimmedText(), 20),
            OwnerId = TruncateOrNull(part.Owner.GetFirstUserId(), 50),
            ConvertedContactItemId = TruncateOrNull(part.ConvertedContactItemId, 26),
            ConvertedUtc = part.ConvertedUtc,
            CreatedUtc = contentItem.CreatedUtc,
        };
    }
}
