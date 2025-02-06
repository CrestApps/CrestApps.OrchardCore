using CrestApps.OrchardCore.AI.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDeploymentStore : IAIDeploymentStore
{
    private readonly IDocumentManager<AIDeploymentDocument> _documentManager;

    public DefaultAIDeploymentStore(IDocumentManager<AIDeploymentDocument> documentManager)
    {
        _documentManager = documentManager;
    }

    public async ValueTask<bool> DeleteAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var document = await _documentManager.GetOrCreateMutableAsync();

        var removed = document.Deployments.Remove(deployment.Id);

        if (removed)
        {
            await _documentManager.UpdateAsync(document);
        }

        return removed;
    }

    public async ValueTask<AIDeployment> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (document.Deployments.TryGetValue(id, out var profile))
        {
            return profile;
        }

        return null;
    }

    public async ValueTask<AIDeployment> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        var deployment = document.Deployments.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (deployment is not null)
        {
            return deployment;
        }

        return null;
    }

    public async ValueTask SaveAsync(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(deployment.Id))
        {
            deployment.Id = IdGenerator.GenerateId();
        }

        if (document.Deployments.Values.Any(x => x.ProviderName == deployment.ProviderName && x.ConnectionName == deployment.ConnectionName && x.Name.Equals(deployment.Name, StringComparison.OrdinalIgnoreCase) && x.Id != deployment.Id))
        {
            throw new InvalidOperationException("There is already another deployment with the same name.");
        }

        document.Deployments[deployment.Id] = deployment;

        await _documentManager.UpdateAsync(document);
    }

    public async ValueTask<AIDeploymentResult> PageAsync(int page, int pageSize, QueryContext context)
    {
        var records = await LocateQueriesAsync(context);

        var skip = (page - 1) * pageSize;

        return new AIDeploymentResult
        {
            Count = records.Count(),
            Deployments = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllAsync()
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Deployments.Values;
    }

    private async ValueTask<IEnumerable<AIDeployment>> LocateQueriesAsync(QueryContext context)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Deployments.Values;
        }

        var queries = document.Deployments.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(context.Source))
        {
            queries = queries.Where(x => x.ProviderName.Equals(context.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            queries = queries.Where(x => x.Name.Contains(context.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            queries = queries.OrderBy(x => x.Name);
        }

        return queries;
    }
}
