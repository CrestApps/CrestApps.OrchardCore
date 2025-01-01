using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class DefaultOpenAIChatProfileStore : IOpenAIChatProfileStore
{
    private readonly IDocumentManager<OpenAIChatProfileDocument> _documentManager;

    public DefaultOpenAIChatProfileStore(IDocumentManager<OpenAIChatProfileDocument> documentManager)
    {
        _documentManager = documentManager;
    }

    public async Task<bool> DeleteAsync(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = await _documentManager.GetOrCreateMutableAsync();

        var removed = document.Profiles.Remove(profile.Id);

        if (removed)
        {
            await _documentManager.UpdateAsync(document);
        }

        return removed;
    }

    public async Task<OpenAIChatProfile> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (document.Profiles.TryGetValue(id, out var profile))
        {
            return profile;
        }

        return null;
    }

    public async Task<OpenAIChatProfile> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        var profile = document.Profiles.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (profile is not null)
        {
            return profile;
        }

        return null;
    }

    public async Task SaveAsync(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = IdGenerator.GenerateId();
        }

        if (document.Profiles.Values.Any(x => x.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase) && x.Id != profile.Id))
        {
            throw new InvalidOperationException("The is already another profile with the same name.");
        }

        document.Profiles[profile.Id] = profile;

        await _documentManager.UpdateAsync(document);
    }

    public async Task<OpenAIChatProfileResult> PageAsync(int page, int pageSize, QueryContext context)
    {
        var records = await LocateQueriesAsync(context);

        var skip = (page - 1) * pageSize;

        return new OpenAIChatProfileResult
        {
            Count = records.Count(),
            Profiles = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async Task<IEnumerable<OpenAIChatProfile>> GetAllAsync()
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Profiles.Values;
    }

    private async Task<IEnumerable<OpenAIChatProfile>> LocateQueriesAsync(QueryContext context)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Profiles.Values;
        }

        var queries = document.Profiles.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(context.Source))
        {
            queries = queries.Where(x => x.Source.Equals(context.Source, StringComparison.OrdinalIgnoreCase));
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


public sealed class DefaultOpenAIDeploymentStore : IOpenAIDeploymentStore
{
    private readonly IDocumentManager<OpenAIDeploymentDocument> _documentManager;

    public DefaultOpenAIDeploymentStore(IDocumentManager<OpenAIDeploymentDocument> documentManager)
    {
        _documentManager = documentManager;
    }

    public async Task<bool> DeleteAsync(OpenAIDeployment deployment)
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

    public async Task<OpenAIDeployment> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (document.Deployments.TryGetValue(id, out var profile))
        {
            return profile;
        }

        return null;
    }

    public async Task<OpenAIDeployment> FindByNameAsync(string name)
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

    public async Task SaveAsync(OpenAIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(deployment.Id))
        {
            deployment.Id = IdGenerator.GenerateId();
        }

        if (document.Deployments.Values.Any(x => x.Name.Equals(deployment.Name, StringComparison.OrdinalIgnoreCase) && x.Id != deployment.Id))
        {
            throw new InvalidOperationException("The is already another deployment with the same name.");
        }

        document.Deployments[deployment.Id] = deployment;

        await _documentManager.UpdateAsync(document);
    }

    public async Task<OpenAIDeploymentResult> PageAsync(int page, int pageSize, QueryContext context)
    {
        var records = await LocateQueriesAsync(context);

        var skip = (page - 1) * pageSize;

        return new OpenAIDeploymentResult
        {
            Count = records.Count(),
            Deployments = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async Task<IEnumerable<OpenAIDeployment>> GetAllAsync()
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Deployments.Values;
    }

    private async Task<IEnumerable<OpenAIDeployment>> LocateQueriesAsync(QueryContext context)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Deployments.Values;
        }

        var queries = document.Deployments.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(context.Source))
        {
            queries = queries.Where(x => x.Source.Equals(context.Source, StringComparison.OrdinalIgnoreCase));
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
