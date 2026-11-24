using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Core.Indexes;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.BusinessDocuments.Indexes;

internal sealed class BusinessDocumentTemplateIndexProvider : IndexProvider<BusinessDocumentTemplate>
{
    public BusinessDocumentTemplateIndexProvider()
    {
        CollectionName = BusinessDocumentsStorage.CollectionName;
    }

    public override void Describe(DescribeContext<BusinessDocumentTemplate> context)
    {
        context
            .For<BusinessDocumentTemplateIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(template => new BusinessDocumentTemplateIndex
            {
                ItemId = template.ItemId,
                Name = template.Name,
                IsDefault = template.IsDefault,
            });
    }
}
