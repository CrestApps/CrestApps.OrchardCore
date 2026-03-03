namespace CrestApps.Services;

public interface ISourceCatalog<T> : ICatalog<T>
    where T : ISourceAwareModel
{
    ValueTask<IReadOnlyCollection<T>> GetAsync(string source);
}
