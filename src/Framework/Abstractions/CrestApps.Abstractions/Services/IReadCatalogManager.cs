using CrestApps.Models;

namespace CrestApps.Services;

public interface IReadCatalogManager<T>
{
    ValueTask<T> FindByIdAsync(string id);
    ValueTask<IEnumerable<T>> GetAllAsync();
    ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext;
}
