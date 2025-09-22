using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIProviderConnectionStore : NamedCatalog<AIProviderConnection>
{
    public AIProviderConnectionStore(IDocumentManager<DictionaryDocument<AIProviderConnection>> documentManager)
        : base(documentManager)
    {
    }

    protected override void Saving(AIProviderConnection record, DictionaryDocument<AIProviderConnection> document)
    {
        base.Saving(record, document);

        if (record.IsDefault)
        {
            var previousModels = document.Records.Values.Where(r => r.IsDefault && r.ItemId != record.ItemId);

            foreach (var model in previousModels)
            {
                model.IsDefault = false;
            }
        }
    }
}
