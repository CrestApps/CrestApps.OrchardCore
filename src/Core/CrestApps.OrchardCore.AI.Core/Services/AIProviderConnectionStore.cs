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
}
