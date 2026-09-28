using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Wraps a contribution store and runs supplied actions the moment a read returns. The roller reads a batch and
/// then deletes it, and whether that delete may be written as a predicate depends entirely on what happens to a
/// contribution appended between those two steps. The rebuild walks contributions a page at a time while the
/// roller is free to fold and delete underneath it, and whether that walk may be written as offset paging
/// depends entirely on what happens between two pages. Neither window is wide enough to be hit by chance, so
/// both are opened on purpose rather than waited for.
/// </summary>
internal sealed class InterleavingMetricDeltaStore : IContactCenterMetricDeltaStore
{
    private readonly IContactCenterMetricDeltaStore _inner;
    private readonly Func<Task> _afterBatchRead;
    private readonly Func<Task> _afterContributionPage;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterleavingMetricDeltaStore"/> class.
    /// </summary>
    /// <param name="inner">The real store every call is delegated to.</param>
    /// <param name="afterBatchRead">The work to run once, immediately after the first batch read returns rows.</param>
    /// <param name="afterContributionPage">The work to run once, immediately after the first contribution page returns rows.</param>
    public InterleavingMetricDeltaStore(
        IContactCenterMetricDeltaStore inner,
        Func<Task> afterBatchRead = null,
        Func<Task> afterContributionPage = null)
    {
        _inner = inner;
        _afterBatchRead = afterBatchRead;
        _afterContributionPage = afterContributionPage;
    }

    /// <summary>
    /// Gets the number of times the interleaved fold actually ran. An assertion about what survives the fold is
    /// meaningless if nothing was ever appended into the window.
    /// </summary>
    public int Interleaved { get; private set; }

    /// <summary>
    /// Gets the number of contribution pages the walk actually read. An assertion about paging is meaningless
    /// if the walk only ever read one page.
    /// </summary>
    public int ContributionPagesRead { get; private set; }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContactCenterEventMetricDelta>> GetBatchAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var batch = await _inner.GetBatchAsync(maxCount, cancellationToken);

        if (_afterBatchRead is not null && batch.Count > 0 && Interleaved == 0)
        {
            await _afterBatchRead();
            Interleaved++;
        }

        return batch;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<ContactCenterEventMetricDelta>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => _inner.GetByDateRangeAsync(fromUtc, toUtc, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContactCenterMetricContribution>> GetContributionsAfterAsync(long afterDocumentId, int count, CancellationToken cancellationToken = default)
    {
        var page = await _inner.GetContributionsAfterAsync(afterDocumentId, count, cancellationToken);

        if (page.Count > 0)
        {
            ContributionPagesRead++;

            if (_afterContributionPage is not null && ContributionPagesRead == 1)
            {
                await _afterContributionPage();
            }
        }

        return page;
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(ContactCenterEventMetricDelta record, CancellationToken cancellationToken = default)
        => _inner.CreateAsync(record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask UpdateAsync(ContactCenterEventMetricDelta record, CancellationToken cancellationToken = default)
        => _inner.UpdateAsync(record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<bool> DeleteAsync(ContactCenterEventMetricDelta record, CancellationToken cancellationToken = default)
        => _inner.DeleteAsync(record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ContactCenterEventMetricDelta> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => _inner.FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<ContactCenterEventMetricDelta>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => _inner.GetAsync(ids, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<ContactCenterEventMetricDelta>> GetAllAsync(CancellationToken cancellationToken = default)
        => _inner.GetAllAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PageResult<ContactCenterEventMetricDelta>> PageAsync<TQuery>(
        int page,
        int pageSize,
        TQuery context,
        CancellationToken cancellationToken = default)
        where TQuery : QueryContext
        => _inner.PageAsync(page, pageSize, context, cancellationToken);
}
