using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;
using static CrestApps.OrchardCore.StringTruncation;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class LeadImportIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<LeadImportIndex>()
            .Map(CreateIndexes);
    }

    internal static IEnumerable<LeadImportIndex> CreateIndexes(ContentItem contentItem)
    {
        if ((!contentItem.Published && !contentItem.Latest) ||
            !contentItem.TryGet<LeadPart>(out var part) ||
            part.Imports is not { Count: > 0 })
        {
            return [];
        }

        return part.Imports
            .Where(import => !string.IsNullOrEmpty(import?.EntryId))
            .DistinctBy(import => import.EntryId, StringComparer.Ordinal)
            .Select(import => new LeadImportIndex
            {
                ContentItemId = contentItem.ContentItemId,
                ContentType = TruncateOrNull(contentItem.ContentType, 255),
                Published = contentItem.Published,
                Latest = contentItem.Latest,
                EntryId = TruncateOrNull(import.EntryId, 26),
                FileName = TruncateOrNull(import.FileName, 255),
                ImportedUtc = import.ImportedUtc,
            })
            .ToArray();
    }
}
