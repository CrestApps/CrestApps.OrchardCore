using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public class NamedSourceDocumentCatalog<T> : SourceDocumentCatalog<T>, INamedSourceCatalog<T>, INamedCatalog<T>
    where T : CatalogItem, INameAwareModel, ISourceAwareModel
{
    public NamedSourceDocumentCatalog(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var record = await GetReadQuery()
            .FirstOrDefaultAsync(x => x.Name == name);

        return record is null ? null : CatalogRecordFactory.Materialize<T>(record);
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(source);

        var record = await GetReadQuery()
            .FirstOrDefaultAsync(x => x.Name == name && x.Source == source);

        return record is null ? null : CatalogRecordFactory.Materialize<T>(record);
    }

    protected override async ValueTask SavingAsync(T record)
    {
        var exists = await GetReadQuery()
            .AnyAsync(x => x.Name == record.Name && x.ItemId != record.ItemId);

        if (exists)
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }
}
