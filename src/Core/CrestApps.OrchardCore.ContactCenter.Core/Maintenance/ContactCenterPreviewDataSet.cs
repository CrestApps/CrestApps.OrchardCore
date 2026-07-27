using CrestApps.OrchardCore.ContactCenter.Maintenance;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Maintenance;

/// <summary>
/// Provides a YesSql-backed <see cref="IContactCenterPreviewDataSet"/> for a single persisted Contact Center
/// document type. One instance is registered per persisted document type so the export and reset tooling is
/// driven by the same registry the completeness gate checks, rather than by a hand-written list per operation.
/// </summary>
/// <typeparam name="TDocument">The persisted document type.</typeparam>
public sealed class ContactCenterPreviewDataSet<TDocument> : IContactCenterPreviewDataSet
    where TDocument : class
{
    private const int MaxDeleteBatches = 10_000;

    private readonly ISession _session;
    private readonly int _pageSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterPreviewDataSet{TDocument}"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="governanceCategoryKey">The governance catalog category key that classifies this data set.</param>
    /// <param name="isConfiguration">Whether this data set holds operator-authored configuration.</param>
    /// <param name="pageSize">The number of documents read per page while exporting or deleting.</param>
    public ContactCenterPreviewDataSet(
        ISession session,
        string governanceCategoryKey,
        bool isConfiguration,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(governanceCategoryKey);

        _session = session;
        _pageSize = pageSize <= 0 ? 200 : pageSize;
        GovernanceCategoryKey = governanceCategoryKey;
        IsConfiguration = isConfiguration;
    }

    /// <inheritdoc/>
    public string Key => typeof(TDocument).Name;

    /// <inheritdoc/>
    public string GovernanceCategoryKey { get; }

    /// <inheritdoc/>
    public bool IsConfiguration { get; }

    /// <inheritdoc/>
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _session.Query<TDocument>(collection: ContactCenterConstants.CollectionName).CountAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<object>> ReadPageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var boundedSkip = skip < 0 ? 0 : skip;
        var boundedTake = take <= 0 ? _pageSize : take;

        var documents = await _session.Query<TDocument>(collection: ContactCenterConstants.CollectionName)
            .Skip(boundedSkip)
            .Take(boundedTake)
            .ListAsync(cancellationToken);

        return documents.Cast<object>().ToArray();
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var deleted = 0;

        // Each batch re-reads the head of the data set rather than advancing an offset, because the previous
        // batch removed the rows it read. Advancing an offset would step over undeleted rows.
        for (var batch = 0; batch < MaxDeleteBatches; batch++)
        {
            var documents = await _session.Query<TDocument>(collection: ContactCenterConstants.CollectionName)
                .Take(_pageSize)
                .ListAsync(cancellationToken);

            var page = documents as ICollection<TDocument> ?? documents.ToArray();

            if (page.Count == 0)
            {
                break;
            }

            foreach (var document in page)
            {
                _session.Delete(document, collection: ContactCenterConstants.CollectionName);
                deleted++;
            }

            await _session.FlushAsync(cancellationToken);
        }

        return deleted;
    }
}
