namespace CrestApps.Services;

public interface INamedCatalogManager<T> : ICatalogManager<T>
    where T : INameAwareModel
{
    ValueTask<T> FindByNameAsync(string name);
}
