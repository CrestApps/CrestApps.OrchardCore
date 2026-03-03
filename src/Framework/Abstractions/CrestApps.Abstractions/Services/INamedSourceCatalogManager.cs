namespace CrestApps.Services;

public interface INamedSourceCatalogManager<T> : INamedCatalogManager<T>, ISourceCatalogManager<T>
    where T : INameAwareModel, ISourceAwareModel
{
    ValueTask<T> GetAsync(string name, string source);
}
