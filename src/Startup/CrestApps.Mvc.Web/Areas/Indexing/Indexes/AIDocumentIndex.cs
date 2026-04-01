using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class AIDocumentIndex : CatalogItemIndex
{
    public string ReferenceId { get; set; }

    public string ReferenceType { get; set; }

    public string FileName { get; set; }
}

public sealed class AIDocumentIndexProvider : IndexProvider<AIDocument>
{
    public override void Describe(DescribeContext<AIDocument> context)
    {
        context.For<AIDocumentIndex>()
            .Map(doc => new AIDocumentIndex
            {
                ItemId = doc.ItemId,
                ReferenceId = doc.ReferenceId,
                ReferenceType = doc.ReferenceType,
                FileName = doc.FileName,
            });
    }
}
