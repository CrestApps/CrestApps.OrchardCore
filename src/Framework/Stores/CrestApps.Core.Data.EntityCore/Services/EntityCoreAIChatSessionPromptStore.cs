using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using Microsoft.EntityFrameworkCore;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreAIChatSessionPromptStore : DocumentCatalog<AIChatSessionPrompt>, IAIChatSessionPromptStore
{
    public EntityCoreAIChatSessionPromptStore(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var records = await GetReadQuery()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.CreatedUtc)
            .ToListAsync();

        return records
            .Select(CatalogRecordFactory.Materialize<AIChatSessionPrompt>)
            .ToArray();
    }

    public async Task<int> DeleteAllPromptsAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var records = await GetTrackedQuery()
            .Where(x => x.SessionId == sessionId)
            .ToListAsync();

        if (records.Count == 0)
        {
            return 0;
        }

        DbContext.CatalogRecords.RemoveRange(records);
        await DbContext.SaveChangesAsync();

        return records.Count;
    }

    public Task<int> CountAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return GetReadQuery()
            .Where(x => x.SessionId == sessionId)
            .CountAsync();
    }
}
