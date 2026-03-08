using System.Text.Json.Nodes;

namespace CrestApps.Services;

public interface ISourceCatalogManager<T> : ICatalogManager<T>
    where T : ISourceAwareModel
{
    ValueTask<T> NewAsync(string source, JsonNode data = null);
    ValueTask<IEnumerable<T>> GetAsync(string source);
    ValueTask<IEnumerable<T>> FindBySourceAsync(string source);
}
