using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreAIDocumentChunkStore : DocumentCatalog<AIDocumentChunk>, IAIDocumentChunkStore
{
    public EntityCoreAIDocumentChunkStore(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        var records = await GetReadQuery()
            .Where(x => x.AIDocumentId == documentId)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<AIDocumentChunk>)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(string referenceId, string referenceType)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        var records = await GetReadQuery()
            .Where(x => x.ReferenceId == referenceId && x.ReferenceType == referenceType)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<AIDocumentChunk>)
            .ToArray();
    }

    public async Task DeleteByDocumentIdAsync(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        var records = await GetTrackedQuery()
            .Where(x => x.AIDocumentId == documentId)
            .ToListAsync();

        if (records.Count == 0)
        {
            return;
        }

        DbContext.CatalogRecords.RemoveRange(records);
        await DbContext.SaveChangesAsync();
    }
}
