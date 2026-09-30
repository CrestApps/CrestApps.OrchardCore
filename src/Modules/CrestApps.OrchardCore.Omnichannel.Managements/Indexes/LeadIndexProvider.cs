using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

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
            ContentType = Truncate(contentItem.ContentType, 255),
            Published = contentItem.Published,
            Latest = contentItem.Latest,
            StatusId = Truncate(part.StatusId, 50),
            IsClosed = part.IsClosed || part.IsConverted,
            IsConverted = part.IsConverted,
            Source = Truncate(part.Source?.Trim(), 255),
            ListName = Truncate(part.ListName?.Trim(), 255),
            Rating = Truncate(part.Rating, 20),
            OwnerId = Truncate(part.OwnerId, 50),
            ConvertedContactItemId = Truncate(part.ConvertedContactItemId, 26),
            ConvertedUtc = part.ConvertedUtc,
            CreatedUtc = contentItem.CreatedUtc,
        };
    }

    private static string Truncate(string value, int maxLength)
        => string.IsNullOrEmpty(value)
            ? null
            : value[..Math.Min(maxLength, value.Length)];
}
