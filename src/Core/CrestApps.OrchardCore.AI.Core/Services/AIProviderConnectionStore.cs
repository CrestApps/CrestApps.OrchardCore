using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AIProviderConnectionStore : NamedModelStore<AIProviderConnection>
{
    public AIProviderConnectionStore(IDocumentManager<ModelDocument<AIProviderConnection>> documentManager)
        : base(documentManager)
    {
    }

    protected override void Saving(AIProviderConnection record, ModelDocument<AIProviderConnection> document)
    {
        base.Saving(record, document);

        if (record.IsDefault)
        {
            var previousModels = document.Records.Values.Where(r => r.IsDefault && r.Id != record.Id);

            foreach (var model in previousModels)
            {
                model.IsDefault = false;
            }
        }
    }
}
