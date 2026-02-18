using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Documents.Indexes;

internal sealed class AIDocumentIndexProvider : IndexProvider<AIDocument>
{
    public AIDocumentIndexProvider()
    {
        CollectionName = AIConstants.CollectionName;
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
