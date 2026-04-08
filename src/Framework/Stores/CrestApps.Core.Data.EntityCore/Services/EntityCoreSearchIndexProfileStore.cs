using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreSearchIndexProfileStore : NamedDocumentCatalog<SearchIndexProfile>, ISearchIndexProfileStore
{
    public EntityCoreSearchIndexProfileStore(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }

    public new async Task<SearchIndexProfile> FindByNameAsync(string name)
        => await base.FindByNameAsync(name);

    public async Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        var records = await GetReadQuery()
            .Where(x => x.Type == type)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<SearchIndexProfile>)
            .ToArray();
    }
}
