using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Accounts;

/// <summary>
/// Keeps content definitions in memory, so the code that reads and alters them runs against real definitions.
/// </summary>
internal sealed class InMemoryContentDefinitionManager : IContentDefinitionManager
{
    private readonly Dictionary<string, ContentTypeDefinition> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContentPartDefinition> _parts = new(StringComparer.Ordinal);

    public int TypeStores { get; private set; }

    public Task<IEnumerable<ContentTypeDefinition>> LoadTypeDefinitionsAsync()
        => Task.FromResult<IEnumerable<ContentTypeDefinition>>(_types.Values.ToArray());

    public Task<IEnumerable<ContentTypeDefinition>> ListTypeDefinitionsAsync()
        => LoadTypeDefinitionsAsync();

    public Task<IEnumerable<ContentPartDefinition>> LoadPartDefinitionsAsync()
        => Task.FromResult<IEnumerable<ContentPartDefinition>>(_parts.Values.ToArray());

    public Task<IEnumerable<ContentPartDefinition>> ListPartDefinitionsAsync()
        => LoadPartDefinitionsAsync();

    public Task<ContentTypeDefinition> LoadTypeDefinitionAsync(string name)
        => Task.FromResult(_types.GetValueOrDefault(name));

    public Task<ContentTypeDefinition> GetTypeDefinitionAsync(string name)
        => LoadTypeDefinitionAsync(name);

    public Task<ContentPartDefinition> LoadPartDefinitionAsync(string name)
        => Task.FromResult(_parts.GetValueOrDefault(name));

    public Task<ContentPartDefinition> GetPartDefinitionAsync(string name)
        => LoadPartDefinitionAsync(name);

    public Task DeleteTypeDefinitionAsync(string name)
    {
        _types.Remove(name);

        return Task.CompletedTask;
    }

    public Task DeletePartDefinitionAsync(string name)
    {
        _parts.Remove(name);

        return Task.CompletedTask;
    }

    public Task StoreTypeDefinitionAsync(ContentTypeDefinition contentTypeDefinition)
    {
        TypeStores++;
        _types[contentTypeDefinition.Name] = contentTypeDefinition;

        return Task.CompletedTask;
    }

    public Task StorePartDefinitionAsync(ContentPartDefinition contentPartDefinition)
    {
        _parts[contentPartDefinition.Name] = contentPartDefinition;

        return Task.CompletedTask;
    }

    public Task<string> GetIdentifierAsync()
        => Task.FromResult("test");
}
