using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDataSourceStore : Catalog<AIDataSource>
{
    public DefaultAIDataSourceStore(IDocumentManager<DictionaryDocument<AIDataSource>> documentManager)
        : base(documentManager)
    {
    }

    protected override IEnumerable<AIDataSource> GetSortable(QueryContext context, IEnumerable<AIDataSource> records)
    {
        if (!string.IsNullOrEmpty(context.Name))
        {
            records = records.Where(x => context.Name.Contains(x.DisplayText, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            records = records.OrderBy(x => x.DisplayText);
        }

        return records;
    }
}
