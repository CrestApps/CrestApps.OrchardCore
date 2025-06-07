using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Migrations;

[Obsolete("This class will be removed before the v1 is released.")]
internal sealed class DeploymentStoreMigrations : DataMigration
{
    private readonly INamedCatalog<AIDeployment> _deploymentCatalog;
    private readonly IDocumentManager<AIDeploymentDocument> _deploymentDocument;

    public DeploymentStoreMigrations(
        INamedCatalog<AIDeployment> deploymentCatalog,
        IDocumentManager<AIDeploymentDocument> deploymentDocument)
    {
        _deploymentCatalog = deploymentCatalog;
        _deploymentDocument = deploymentDocument;
    }

    public async Task<int> CreateAsync()
    {
        var deploymentDocument = await _deploymentDocument.GetOrCreateImmutableAsync();

        foreach (var deployment in deploymentDocument.Deployments.Values)
        {
            try
            {
                await _deploymentCatalog.CreateAsync(deployment);
                await _deploymentCatalog.SaveChangesAsync();
            }
            catch { }
        }

        return 1;
    }
}
