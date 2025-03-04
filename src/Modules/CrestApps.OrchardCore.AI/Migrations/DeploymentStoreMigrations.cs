using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Migrations;

[Obsolete("This class will be removed before the v1 is released.")]
internal sealed class DeploymentStoreMigrations : DataMigration
{
    private readonly INamedModelStore<AIDeployment> _deploymentsStore;
    private readonly IDocumentManager<AIDeploymentDocument> _deploymentDocument;

    public DeploymentStoreMigrations(
        INamedModelStore<AIDeployment> deploymentsStore,
        IDocumentManager<AIDeploymentDocument> deploymentDocument)
    {
        _deploymentsStore = deploymentsStore;
        _deploymentDocument = deploymentDocument;
    }

    public async Task<int> CreateAsync()
    {
        var deploymentDocument = await _deploymentDocument.GetOrCreateImmutableAsync();

        foreach (var deployment in deploymentDocument.Deployments.Values)
        {
            try
            {
                await _deploymentsStore.SaveAsync(deployment);
            }
            catch { }
        }

        return 1;
    }
}
