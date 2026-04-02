using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Models;
using CrestApps.Services;

namespace CrestApps.Infrastructure.Indexing;

public interface IIndexProfileHandler : ICatalogEntryHandler<SearchIndexProfile>
{
    ValueTask ValidateAsync(
        SearchIndexProfile indexProfile,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyCollection<SearchIndexField>> GetFieldsAsync(
        SearchIndexProfile indexProfile,
        CancellationToken cancellationToken = default);

    Task SynchronizedAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default);

    Task ResetAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default);

    Task DeletingAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default);
}
