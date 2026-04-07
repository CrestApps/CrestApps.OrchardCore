using CrestApps.Handlers;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Models;

namespace CrestApps.AI.Indexing;

public abstract class IndexProfileHandlerBase : CatalogEntryHandlerBase<SearchIndexProfile>, IIndexProfileHandler
{
    public virtual ValueTask ValidateAsync(
        SearchIndexProfile indexProfile,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask<IReadOnlyCollection<SearchIndexField>> GetFieldsAsync(
        SearchIndexProfile indexProfile,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<SearchIndexField>>(null);

    public virtual Task SynchronizedAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task ResetAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task DeletingAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public override async Task ValidatingAsync(ValidatingContext<SearchIndexProfile> context)
        => await ValidateAsync(context.Model, context.Result);

    public override async Task DeletingAsync(DeletingContext<SearchIndexProfile> context)
        => await DeletingAsync(context.Model);
}
