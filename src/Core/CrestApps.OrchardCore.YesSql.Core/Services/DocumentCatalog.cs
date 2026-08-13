using CrestApps.Core;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.YesSql.Core.Services;

/// <summary>
/// YesSql-backed implementation of <see cref="ICatalog{T}"/> that stores catalog entries
/// as individual YesSql documents with a corresponding index.
/// </summary>
/// <typeparam name="T">The type of catalog item managed by this catalog.</typeparam>
/// <typeparam name="TIndex">The YesSql index type used to query catalog items.</typeparam>
public class DocumentCatalog<T, TIndex> : ICatalog<T>
    where T : CatalogItem
    where TIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the YesSql collection name used for storing documents.
    /// </summary>
    protected string CollectionName { get; set; }

    /// <summary>
    /// Gets a value indicating whether updates use YesSql document-version concurrency checks.
    /// </summary>
    protected virtual bool CheckConcurrency => false;

    /// <summary>
    /// The YesSql session used for database operations.
    /// </summary>
    protected readonly ISession Session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentCatalog{T, TIndex}"/> class.
    /// </summary>
    /// <param name="session">The YesSql session for database access.</param>
    public DocumentCatalog(ISession session)
    {
        Session = session;
    }

    internal DocumentCatalog(
        ISession session,
        string collectionName)
        : this(session)
    {
        CollectionName = collectionName;
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(T entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await DeletingAsync(entry);

        Session.Delete(entry, CollectionName);

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var item = await Session.Query<T, TIndex>(x => x.ItemId == id, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

        return await LoadedAsync(item);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var items = await Session.Query<T, TIndex>(x => x.ItemId.IsIn(ids), collection: CollectionName).ListAsync(cancellationToken);

        return await LoadedAsync(items);
    }

    /// <inheritdoc />
    public async ValueTask<PageResult<T>> PageAsync<TQuery>(
        int page,
        int pageSize,
        TQuery context,
        CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        IQuery<T> query = Session.Query<T, TIndex>(collection: CollectionName);

        if (context is not null)
        {
            if (!string.IsNullOrEmpty(context.Name))
            {
                if (typeof(INameAwareIndex).IsAssignableFrom(typeof(TIndex)))
                {
                    if (context.Sorted)
                    {
                        query = query.With<INameAwareIndex>(x => x.Name.Contains(context.Name))
                            .OrderBy(x => x.Name);
                    }
                    else
                    {
                        query = query.With<INameAwareIndex>(x => x.Name.Contains(context.Name));
                    }
                }
                else if (typeof(IDisplayTextAwareIndex).IsAssignableFrom(typeof(TIndex)))
                {
                    if (context.Sorted)
                    {
                        query = query.With<IDisplayTextAwareIndex>(x => x.DisplayText.Contains(context.Name))
                            .OrderBy(x => x.DisplayText);
                    }
                    else
                    {
                        query = query.With<IDisplayTextAwareIndex>(x => x.DisplayText.Contains(context.Name));
                    }
                }
            }

            if (!string.IsNullOrEmpty(context.Source) && typeof(ISourceAwareIndex).IsAssignableFrom(typeof(TIndex)))
            {
                query = query.With<ISourceAwareIndex>(x => x.Source == context.Source);
            }

            await PagingAsync(query, context);
        }

        var skip = (page - 1) * pageSize;

        return new PageResult<T>
        {
            Count = await query.CountAsync(cancellationToken),
            Entries = await LoadedAsync(await query.Skip(skip).Take(pageSize).ListAsync(cancellationToken))
        };
    }

    protected virtual ValueTask PagingAsync<TQuery>(IQuery<T> query, TQuery context)
        where TQuery : QueryContext
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<T, TIndex>(collection: CollectionName).ListAsync(cancellationToken);

        return await LoadedAsync(items);
    }

    /// <inheritdoc />
    public async ValueTask CreateAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await SavingAsync(record);

        await Session.SaveAsync(
            record,
            collection: CollectionName,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(T record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await SavingAsync(record);

        await Session.SaveAsync(
            record,
            checkConcurrency: CheckConcurrency,
            collection: CollectionName,
            cancellationToken: cancellationToken);
    }

    protected virtual ValueTask DeletingAsync(T model)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SavingAsync(T record)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called for every record materialized from storage before it is returned to a caller. Derived catalogs
    /// override this to reconcile a persisted document with the shape the running code expects, so a document
    /// written by an earlier release is never handed to a caller in its stored shape.
    /// </summary>
    /// <param name="record">The record read from storage.</param>
    /// <returns>A task that completes when the record has been reconciled.</returns>
    protected virtual ValueTask LoadingAsync(T record)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Applies <see cref="LoadingAsync(T)"/> to a record read from storage.
    /// </summary>
    /// <param name="record">The record read from storage, which may be <see langword="null"/>.</param>
    /// <returns>The same record, after reconciliation.</returns>
    protected async ValueTask<T> LoadedAsync(T record)
    {
        if (record is null)
        {
            return null;
        }

        await LoadingAsync(record);

        return record;
    }

    /// <summary>
    /// Applies <see cref="LoadingAsync(T)"/> to every record in a sequence read from storage.
    /// </summary>
    /// <param name="records">The records read from storage.</param>
    /// <returns>The records, as an array, after reconciliation.</returns>
    protected async ValueTask<T[]> LoadedAsync(IEnumerable<T> records)
    {
        var materialized = records as T[] ?? records.ToArray();

        foreach (var record in materialized)
        {
            if (record is null)
            {
                continue;
            }

            await LoadingAsync(record);
        }

        return materialized;
    }
}
