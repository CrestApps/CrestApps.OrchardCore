using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreAIDocumentStore : DocumentCatalog<AIDocument>, IAIDocumentStore
{
    public EntityCoreAIDocumentStore(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(string referenceId, string referenceType)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        var records = await GetReadQuery()
            .Where(x => x.ReferenceId == referenceId && x.ReferenceType == referenceType)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<AIDocument>)
            .ToArray();
    }
}
