using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the default AI data source store.
/// </summary>
public sealed class DefaultAIDataSourceStore : Catalog<AIDataSource>, IAIDataSourceStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIDataSourceStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager.</param>
    public DefaultAIDataSourceStore(IDocumentManager<DictionaryDocument<AIDataSource>> documentManager)
        : base(documentManager)
    {
    }

    protected override IEnumerable<AIDataSource> GetSortable(QueryContext context, IEnumerable<AIDataSource> records)
    {
        if (!string.IsNullOrEmpty(context.Name))
        {
            records = records.Where(x => x.DisplayText.Contains(context.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            records = records.OrderBy(x => x.DisplayText);
        }

        return records;
    }
}
