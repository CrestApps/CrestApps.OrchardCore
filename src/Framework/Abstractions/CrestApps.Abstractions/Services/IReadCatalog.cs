using CrestApps.Models;

namespace CrestApps.Services;

public interface IReadCatalog<T>
{
    ValueTask<T> FindByIdAsync(string id);
    ValueTask<IReadOnlyCollection<T>> GetAllAsync();
    ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids);
    ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext;
}
