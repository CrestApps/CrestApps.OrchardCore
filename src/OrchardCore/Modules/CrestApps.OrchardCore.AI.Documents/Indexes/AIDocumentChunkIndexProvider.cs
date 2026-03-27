using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Documents.Indexes;

internal sealed class AIDocumentChunkIndexProvider : IndexProvider<AIDocumentChunk>
{
    public AIDocumentChunkIndexProvider()
    {
        CollectionName = AIConstants.AIDocsCollectionName;
    }

    public override void Describe(DescribeContext<AIDocumentChunk> context)
    {
        context
            .For<AIDocumentChunkIndex>()
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
