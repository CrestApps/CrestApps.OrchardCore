using CrestApps.OrchardCore.AI.Azure.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIChatProfileStore : IAIChatProfileStore
{
    private readonly IDocumentManager<AIChatProfileDocument> _documentManager;

    public DefaultAIChatProfileStore(IDocumentManager<AIChatProfileDocument> documentManager)
    {
        _documentManager = documentManager;
    }

    public async ValueTask<bool> DeleteAsync(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (!document.Profiles.TryGetValue(profile.Id, out var existingProfile))
        {
            return false;
        }

        var settings = existingProfile.GetSettings<AIChatProfileSettings>();

        if (!settings.IsRemovable)
        {
            throw new InvalidOperationException("The profile cannot be removed.");
        }

        var removed = document.Profiles.Remove(profile.Id);

        if (removed)
        {
            await _documentManager.UpdateAsync(document);
        }

        return removed;
    }

    public async ValueTask<AIChatProfile> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (document.Profiles.TryGetValue(id, out var profile))
        {
            return profile;
        }

        return null;
    }

    public async ValueTask<AIChatProfile> FindByNameAsync(string name)
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

    public async ValueTask SaveAsync(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = await _documentManager.GetOrCreateMutableAsync();

        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = IdGenerator.GenerateId();
        }

        if (document.Profiles.Values.Any(x => x.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase) && x.Id != profile.Id))
        {
            throw new InvalidOperationException("There is already another profile with the same name.");
        }

        document.Profiles[profile.Id] = profile;

        await _documentManager.UpdateAsync(document);
    }

    public async ValueTask<AIChatProfileResult> PageAsync(int page, int pageSize, AIChatProfileQueryContext context)
    {
        var records = await LocateQueriesAsync(context);

        var skip = (page - 1) * pageSize;

        return new AIChatProfileResult
        {
            Count = records.Count(),
            Profiles = records.Skip(skip).Take(pageSize).ToArray()
        };
    }

    public async ValueTask<IEnumerable<AIChatProfile>> GetAllAsync()
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Profiles.Values;
    }

    private async ValueTask<IEnumerable<AIChatProfile>> LocateQueriesAsync(AIChatProfileQueryContext context)
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

        if (context.IsListableOnly)
        {
            queries = queries.Where(x => x.GetSettings<AIChatProfileSettings>().IsListable);
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
