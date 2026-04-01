using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class AIDocumentChunkIndex : CatalogItemIndex
{
    public string AIDocumentId { get; set; }

    public string ReferenceId { get; set; }

    public string ReferenceType { get; set; }

    public int Index { get; set; }
}

public sealed class AIDocumentChunkIndexProvider : IndexProvider<AIDocumentChunk>
{
    public override void Describe(DescribeContext<AIDocumentChunk> context)
    {
        context.For<AIDocumentChunkIndex>()
            .Map(chunk => new AIDocumentChunkIndex
            {
                ItemId = chunk.ItemId,
                AIDocumentId = chunk.AIDocumentId,
                ReferenceId = chunk.ReferenceId,
                ReferenceType = chunk.ReferenceType,
                Index = chunk.Index,
            });
    }
}
