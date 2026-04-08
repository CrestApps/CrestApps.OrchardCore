using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public class SourceDocumentCatalog<T> : DocumentCatalog<T>, ISourceCatalog<T>
    where T : CatalogItem, ISourceAwareModel
{
    public SourceDocumentCatalog(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }

    public async ValueTask<IReadOnlyCollection<T>> GetAsync(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var records = await GetReadQuery()
            .Where(x => x.Source == source)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<T>)
            .ToArray();
    }
}
