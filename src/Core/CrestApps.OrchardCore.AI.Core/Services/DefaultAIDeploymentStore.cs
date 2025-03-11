using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDeploymentStore : NamedSourceModelStore<AIDeployment>
{
    public DefaultAIDeploymentStore(IDocumentManager<ModelDocument<AIDeployment>> documentManager)
        : base(documentManager)
    {
    }

    protected override void Saving(AIDeployment deployment, ModelDocument<AIDeployment> document)
    {
        if (document.Records.Values.Any(x => x.ProviderName == deployment.ProviderName && x.ConnectionName == deployment.ConnectionName && x.Name.Equals(deployment.Name, StringComparison.OrdinalIgnoreCase) && x.Id != deployment.Id))
        {
            throw new InvalidOperationException("There is already another deployment with the same name.");
        }
    }
}
