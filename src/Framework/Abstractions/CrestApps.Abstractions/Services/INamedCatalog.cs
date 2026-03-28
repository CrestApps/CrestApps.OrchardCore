namespace CrestApps.Services;

public interface INamedCatalog<T> : ICatalog<T>
    where T : INameAwareModel
{
    ValueTask<T> FindByNameAsync(string name);
}
