using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIToolInstanceStore : IAIToolInstanceStore
{
    private readonly IDocumentManager<AIToolInstancesDocument> _documentManager;

    public DefaultAIToolInstanceStore(IDocumentManager<AIToolInstancesDocument> documentManager)
    {
        _documentManager = documentManager;
    }

    public async ValueTask<bool> DeleteAsync(AIToolInstance profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (!document.Instances.TryGetValue(profile.Id, out var existingInstance))
        {
            return false;
        }

        var removed = document.Instances.Remove(profile.Id);

        if (removed)
        {
            await _documentManager.UpdateAsync(document);
        }

        return removed;
    }

    public async ValueTask<AIToolInstance> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (document.Instances.TryGetValue(id, out var instance))
        {
            return instance;
        }

        return null;
    }

    public async ValueTask SaveAsync(AIToolInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(instance.Id))
        {
            instance.Id = IdGenerator.GenerateId();
        }

        document.Instances[instance.Id] = instance;

        await _documentManager.UpdateAsync(document);
    }

    public async ValueTask<AIToolInstancesResult> PageAsync(int page, int pageSize, QueryContext context)
    {
        var records = await LocateInstancesAsync(context);

        var skip = (page - 1) * pageSize;

        return new AIToolInstancesResult
        {
            Count = records.Count(),
            Instances = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async ValueTask<IEnumerable<AIToolInstance>> GetAllAsync()
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Instances.Values;
    }

    private async ValueTask<IEnumerable<AIToolInstance>> LocateInstancesAsync(QueryContext context)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (context == null)
        {
            return document.Instances.Values;
        }

        var instances = document.Instances.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(context.Source))
        {
            instances = instances.Where(x => x.Source.Equals(context.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(context.Name))
        {
            instances = instances.Where(x => x.DisplayText.Contains(context.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (context.Sorted)
        {
            instances = instances.OrderBy(x => x.DisplayText);
        }

        return instances;
    }
}
