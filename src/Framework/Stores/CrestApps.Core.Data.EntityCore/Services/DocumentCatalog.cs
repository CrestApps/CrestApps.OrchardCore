using CrestApps.Core.Data.EntityCore.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public class DocumentCatalog<T> : ICatalog<T>
    where T : CatalogItem
{
    protected readonly CrestAppsEntityDbContext DbContext;

    public DocumentCatalog(CrestAppsEntityDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public async ValueTask<bool> DeleteAsync(T entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await DeletingAsync(entry);

        var existing = await GetTrackedQuery()
            .FirstOrDefaultAsync(x => x.ItemId == entry.ItemId);

        if (existing is null)
        {
            return false;
        }

        DbContext.CatalogRecords.Remove(existing);
        await DbContext.SaveChangesAsync();

        return true;
    }

    public async ValueTask<T> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var record = await GetReadQuery()
            .FirstOrDefaultAsync(x => x.ItemId == id);

        return record is null ? null : CatalogRecordFactory.Materialize<T>(record);
    }

    public async ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var itemIds = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();

        if (itemIds.Length == 0)
        {
            return [];
        }

        var records = await GetReadQuery()
            .Where(x => itemIds.Contains(x.ItemId))
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<T>)
            .ToArray();
    }

    public async ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = GetReadQuery();
        var ordered = false;

        if (context is not null)
        {
            if (!string.IsNullOrEmpty(context.Name))
            {
                if (typeof(INameAwareModel).IsAssignableFrom(typeof(T)))
                {
                    query = query.Where(x => x.Name != null && x.Name.Contains(context.Name));

                    if (context.Sorted)
                    {
                        query = query.OrderBy(x => x.Name);
                        ordered = true;
                    }
                }
                else if (typeof(IDisplayTextAwareModel).IsAssignableFrom(typeof(T)))
                {
                    query = query.Where(x => x.DisplayText != null && x.DisplayText.Contains(context.Name));

                    if (context.Sorted)
                    {
                        query = query.OrderBy(x => x.DisplayText);
                        ordered = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(context.Source) && typeof(ISourceAwareModel).IsAssignableFrom(typeof(T)))
            {
                query = query.Where(x => x.Source == context.Source);
            }

            query = ApplyPaging(query, context);
        }

        if (!ordered)
        {
            query = query.OrderBy(x => x.ItemId);
        }

        var skip = (page - 1) * pageSize;
        var count = await query.CountAsync();
        var records = await query.Skip(skip).Take(pageSize).ToListAsync();

        return new PageResult<T>
        {
            Count = count,
            Entries = records.Select(CatalogRecordFactory.Materialize<T>).ToArray(),
        };
    }

    protected virtual IQueryable<CatalogRecord> ApplyPaging<TQuery>(IQueryable<CatalogRecord> query, TQuery context)
        where TQuery : QueryContext
        => query;

    public async ValueTask<IReadOnlyCollection<T>> GetAllAsync()
    {
        var records = await GetReadQuery()
            .OrderBy(x => x.ItemId)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<T>)
            .ToArray();
    }

    public async ValueTask CreateAsync(T record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await SavingAsync(record);

        DbContext.CatalogRecords.Add(CatalogRecordFactory.Create(record));
        await DbContext.SaveChangesAsync();
    }

    public async ValueTask UpdateAsync(T record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await SavingAsync(record);

        var existing = await GetTrackedQuery()
            .FirstOrDefaultAsync(x => x.ItemId == record.ItemId);

        if (existing is null)
        {
            DbContext.CatalogRecords.Add(CatalogRecordFactory.Create(record));
        }
        else
        {
            CatalogRecordFactory.Update(existing, record);
        }

        await DbContext.SaveChangesAsync();
    }

    protected IQueryable<CatalogRecord> GetReadQuery()
        => DbContext.CatalogRecords
            .AsNoTracking()
            .Where(x => x.EntityType == CatalogRecordFactory.GetEntityType<T>());

    protected IQueryable<CatalogRecord> GetTrackedQuery()
        => DbContext.CatalogRecords
            .Where(x => x.EntityType == CatalogRecordFactory.GetEntityType<T>());

    protected virtual ValueTask DeletingAsync(T model)
        => ValueTask.CompletedTask;

    protected virtual ValueTask SavingAsync(T record)
        => ValueTask.CompletedTask;
}
