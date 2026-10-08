using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using YesSql.Indexes;

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
            ContentType = Truncate(contentItem.ContentType, 255),
            Published = contentItem.Published,
            Latest = contentItem.Latest,
            StageId = Truncate(part.StageId, 50),
            IsClosed = part.IsClosed,
            IsWon = part.IsWon,
            Probability = part.Probability,
            Amount = part.Amount?.Value,
            CloseDate = part.CloseDate?.Value,
            OwnerId = Truncate(part.Owner.GetFirstUserId(), 50),
            AccountContentItemId = contentItem.TryGet<ContainedPart>(out var contained)
                ? Truncate(contained.ListContentItemId, 26)
                : null,
            CampaignId = Truncate(part.CampaignId, 50),
            PrimaryContactItemId = Truncate(part.PrimaryContact.GetFirstContentItemId(), 26),
            SourceId = Truncate(part.Source.GetFirstContentItemId(), 26),
            ConvertedFromLeadItemId = Truncate(part.ConvertedFromLeadItemId, 26),
            CreatedUtc = contentItem.CreatedUtc,
        };
    }

    private static string Truncate(string value, int maxLength)
        => string.IsNullOrEmpty(value)
            ? null
            : value[..Math.Min(maxLength, value.Length)];
}
