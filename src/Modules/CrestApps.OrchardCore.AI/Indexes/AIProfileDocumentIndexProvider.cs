using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

internal sealed class AIProfileDocumentIndexProvider : IndexProvider<AIProfileDocument>
{
    public AIProfileDocumentIndexProvider()
    {
        CollectionName = Core.AIConstants.CollectionName;
    }

    public override void Describe(DescribeContext<AIProfileDocument> context)
    {
        context
            .For<AIProfileDocumentIndex>()
            .Map(document =>
            {
                var extension = !string.IsNullOrEmpty(document.FileName)
                    ? Path.GetExtension(document.FileName)
                    : null;

                return new AIProfileDocumentIndex
                {
                    ItemId = document.ItemId,
                    ProfileId = document.ProfileId,
                    Extension = extension,
                };
            });
    }
}
