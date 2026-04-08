using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.Indexing;

public sealed class AIDocumentIndex : CatalogItemIndex
{
    public string ReferenceId { get; set; }

    public string ReferenceType { get; set; }

    public string Extension { get; set; }
}

public sealed class AIDocumentIndexProvider : IndexProvider<AIDocument>
{
    public AIDocumentIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AIDocs;
    }

    public override void Describe(DescribeContext<AIDocument> context)
    {
        context
            .For<AIDocumentIndex>()
            .Map(document =>
            {
                var extension = !string.IsNullOrEmpty(document.FileName)
                ? Path.GetExtension(document.FileName)
                : null;

                return new AIDocumentIndex
                {
                    ItemId = document.ItemId,
                    ReferenceId = document.ReferenceId,
                    ReferenceType = document.ReferenceType,
                    Extension = extension,
                };

            });
    }
}
