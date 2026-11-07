using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using YesSql.Indexes;
using static CrestApps.OrchardCore.StringTruncation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class OpportunityIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OpportunityIndex>()
            .Map(CreateIndex);
    }

    internal static OpportunityIndex CreateIndex(ContentItem contentItem)
    {
        if ((!contentItem.Published && !contentItem.Latest) ||
            !contentItem.TryGet<OpportunityPart>(out var part))
        {
            return null;
        }

        return new OpportunityIndex
        {
            ContentItemId = contentItem.ContentItemId,
            ContentType = TruncateOrNull(contentItem.ContentType, 255),
            Published = contentItem.Published,
            Latest = contentItem.Latest,
            StageId = TruncateOrNull(part.StageId, 50),
            IsClosed = part.IsClosed,
            IsWon = part.IsWon,
            Probability = part.Probability,
            Amount = part.Amount?.Value,
            CloseDate = part.CloseDate?.Value,
            OwnerId = TruncateOrNull(part.Owner.GetFirstUserId(), 50),
            AccountContentItemId = contentItem.TryGet<ContainedPart>(out var contained)
                ? TruncateOrNull(contained.ListContentItemId, 26)
                : null,
            CampaignId = TruncateOrNull(part.CampaignId, 50),
            PrimaryContactItemId = TruncateOrNull(part.PrimaryContact.GetFirstContentItemId(), 26),
            SourceId = TruncateOrNull(part.Source.GetFirstContentItemId(), 26),
            ConvertedFromLeadItemId = TruncateOrNull(part.ConvertedFromLeadItemId, 26),
            CreatedUtc = contentItem.CreatedUtc,
        };
    }
}
