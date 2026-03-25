using CrestApps.OrchardCore.AI.Models;
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

    protected override void Saving(AIDeployment deployment, DictionaryDocument<AIDeployment> document)
    {
        if (document.Records.Values.Any(x => x.ClientName == deployment.ClientName && x.Type == deployment.Type && string.Equals(x.ConnectionName ?? string.Empty, deployment.ConnectionName ?? string.Empty, StringComparison.OrdinalIgnoreCase) && x.Name.Equals(deployment.Name, StringComparison.OrdinalIgnoreCase) && x.ItemId != deployment.ItemId))
        {
            throw new InvalidOperationException("There is already another deployment with the same name.");
        }

        if (deployment.IsDefault)
        {
            var previousDefaults = document.Records.Values
                .Where(x => x.IsDefault &&
                    x.Type == deployment.Type &&
                    string.Equals(x.ConnectionName ?? string.Empty, deployment.ConnectionName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    x.ItemId != deployment.ItemId);

            foreach (var previous in previousDefaults)
            {
                previous.IsDefault = false;
            }
        }
    }
}
