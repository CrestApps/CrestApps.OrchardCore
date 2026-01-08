using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core.Indexes;
using OrchardCore;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.YesSql.Core.Services;

public class DocumentCatalog<T, TIndex> : ICatalog<T>
    where T : CatalogItem
    where TIndex : CatalogItemIndex
{
    protected string CollectionName { get; set; }

    protected readonly ISession Session;

    public DocumentCatalog(ISession session)
    {
        Session = session;
    }

    internal DocumentCatalog(ISession session, string collectionName)
        : this(session)
    {
        CollectionName = collectionName;
    }

    public async ValueTask<bool> DeleteAsync(T entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await DeletingAsync(entry);

        Session.Delete(entry, CollectionName);

        return true;
    }

    public async ValueTask<T> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var item = await Session.Query<T, TIndex>(x => x.ItemId == id, collection: CollectionName).FirstOrDefaultAsync();

        return item;
    }

    public async ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var items = await Session.Query<T, TIndex>(x => x.ItemId.IsIn(ids), collection: CollectionName).ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
            where TQuery : QueryContext
    {
        IQuery<T> query = Session.Query<T, TIndex>(collection: CollectionName);

        if (context is not null)
        {
            if (!string.IsNullOrEmpty(context.Name))
            {
                if (typeof(TIndex).IsAssignableFrom(typeof(INameAwareIndex)))
                {
                    if (context.Sorted)
                    {
                        query = query.With<INameAwareIndex>(x => x.DisplayText.Contains(context.Name))
                            .OrderBy(x => x.DisplayText);
                    }
                    else
                    {
                        query = query.With<INameAwareIndex>(x => x.DisplayText.Contains(context.Name));
                    }
                }
                else if (typeof(TIndex).IsAssignableFrom(typeof(IDisplayTextAwareIndex)))
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

            if (!string.IsNullOrEmpty(context.Source) && typeof(TIndex).IsAssignableFrom(typeof(ISourceAwareIndex)))
            {
                query = query.With<ISourceAwareIndex>(x => x.Source == context.Name);
            }

            await PagingAsync(query, context);
        }

        var skip = (page - 1) * pageSize;

        return new PageResult<T>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray()
        };
    }

    protected virtual ValueTask PagingAsync<TQuery>(IQuery<T> query, TQuery context)
        where TQuery : QueryContext
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IReadOnlyCollection<T>> GetAllAsync()
    {
        var items = await Session.Query<T, TIndex>(collection: CollectionName).ListAsync();

        return items.ToArray();
    }

    public async ValueTask CreateAsync(T record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = IdGenerator.GenerateId();
        }

        await SavingAsync(record);

        await Session.SaveAsync(record, CollectionName);
    }

    public async ValueTask UpdateAsync(T record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = IdGenerator.GenerateId();
        }

        await SavingAsync(record);

        await Session.SaveAsync(record, CollectionName);
    }

    public async ValueTask SaveChangesAsync()
    {
        await Session.FlushAsync();
    }

    protected virtual ValueTask DeletingAsync(T model)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SavingAsync(T record)
    {
        return ValueTask.CompletedTask;
    }
}
