using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.Infrastructure.Indexing;

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
