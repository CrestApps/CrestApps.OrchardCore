using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class SubjectActionCatalog : SourceCatalog<SubjectAction>
{
    public SubjectActionCatalog(IDocumentManager<DictionaryDocument<SubjectAction>> documentManager)
        : base(documentManager)
    {
    }

    protected override IEnumerable<SubjectAction> GetSortable(CrestApps.Core.Models.QueryContext context, IEnumerable<SubjectAction> records)
    {
        if (context is SubjectActionQueryContext subjectContext)
        {
            if (!string.IsNullOrEmpty(subjectContext.SubjectContentType))
            {
                records = records.Where(x => string.Equals(x.SubjectContentType, subjectContext.SubjectContentType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(subjectContext.DispositionId))
            {
                records = records.Where(x => string.Equals(x.DispositionId, subjectContext.DispositionId, StringComparison.OrdinalIgnoreCase));
            }
        }

        return base.GetSortable(context, records);
    }
}
