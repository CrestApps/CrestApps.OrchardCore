using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDeploymentStore : NamedSourceCatalog<AIDeployment>
{
    public DefaultAIDeploymentStore(IDocumentManager<DictionaryDocument<AIDeployment>> documentManager)
        : base(documentManager)
    {
    }
}
