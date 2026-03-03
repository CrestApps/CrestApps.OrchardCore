namespace CrestApps.Services;

public interface INamedSourceCatalog<T> : INamedCatalog<T>, ISourceCatalog<T>
    where T : INameAwareModel, ISourceAwareModel
{
    ValueTask<T> GetAsync(string name, string source);
}
