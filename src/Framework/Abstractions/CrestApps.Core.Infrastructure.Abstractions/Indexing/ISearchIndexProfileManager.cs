using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.Infrastructure.Indexing;

public interface ISearchIndexProfileManager : ICatalogManager<SearchIndexProfile>
{
    Task<SearchIndexProfile> FindByNameAsync(string name);

    Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type);

    ValueTask<IReadOnlyCollection<SearchIndexField>> GetFieldsAsync(
        SearchIndexProfile profile,
        CancellationToken cancellationToken = default);

    Task SynchronizeAsync(SearchIndexProfile profile, CancellationToken cancellationToken = default);

    Task ResetAsync(SearchIndexProfile profile, CancellationToken cancellationToken = default);
}
