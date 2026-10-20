using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// The query of a leads list, counted from the lead index alone.
/// </summary>
/// <remarks>
/// Everything but <see cref="CountAsync"/> goes to the query the content list built, so the list shows exactly the
/// rows it would show; only the count it puts above the pager is read from <see cref="LeadListCountQueryFactory"/>'s
/// query instead.
/// </remarks>
internal sealed class LeadListQuery : IQuery<ContentItem>
{
    private readonly IQuery<ContentItem> _query;
    private readonly IQueryIndex<LeadIndex> _countQuery;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadListQuery"/> class.
    /// </summary>
    /// <param name="query">The query the content list built.</param>
    /// <param name="countQuery">The lead index query that counts the same leads.</param>
    public LeadListQuery(
        IQuery<ContentItem> query,
        IQueryIndex<LeadIndex> countQuery)
    {
        _query = query;
        _countQuery = countQuery;
    }

    /// <inheritdoc/>
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _countQuery.CountAsync(cancellationToken);

    /// <inheritdoc/>
    public IQuery<ContentItem> Any(params Func<IQuery<ContentItem>, IQuery<ContentItem>>[] predicates)
        => _query.Any(predicates);

    /// <inheritdoc/>
    public ValueTask<IQuery<ContentItem>> AnyAsync(params Func<IQuery<ContentItem>, ValueTask<IQuery<ContentItem>>>[] predicates)
        => _query.AnyAsync(predicates);

    /// <inheritdoc/>
    public IQuery<ContentItem> All(params Func<IQuery<ContentItem>, IQuery<ContentItem>>[] predicates)
        => _query.All(predicates);

    /// <inheritdoc/>
    public ValueTask<IQuery<ContentItem>> AllAsync(params Func<IQuery<ContentItem>, ValueTask<IQuery<ContentItem>>>[] predicates)
        => _query.AllAsync(predicates);

    /// <inheritdoc/>
    public IQuery<ContentItem> With(Type indexType)
        => _query.With(indexType);

    /// <inheritdoc/>
    public IQuery<ContentItem, TIndex> With<TIndex>()
        where TIndex : class, IIndex
        => _query.With<TIndex>();

    /// <inheritdoc/>
    public IQuery<ContentItem, TIndex> With<TIndex>(Expression<Func<TIndex, bool>> predicate)
        where TIndex : class, IIndex
        => _query.With(predicate);

    /// <inheritdoc/>
    public IQuery<ContentItem> Skip(int count)
        => _query.Skip(count);

    /// <inheritdoc/>
    public IQuery<ContentItem> Take(int count)
        => _query.Take(count);

    /// <inheritdoc/>
    public Task<ContentItem> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
        => _query.FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ContentItem>> ListAsync(CancellationToken cancellationToken = default)
        => _query.ListAsync(cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<ContentItem> ToAsyncEnumerable(CancellationToken cancellationToken = default)
        => _query.ToAsyncEnumerable(cancellationToken);

    /// <inheritdoc/>
    public string GetTypeAlias(Type t)
        => _query.GetTypeAlias(t);

    /// <inheritdoc/>
    public IQuery<ContentItem> NoDuplicates()
        => _query.NoDuplicates();
}
