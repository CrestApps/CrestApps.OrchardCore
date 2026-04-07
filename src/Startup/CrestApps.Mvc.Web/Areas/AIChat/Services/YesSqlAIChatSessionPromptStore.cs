using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Areas.AIChat.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;
using CrestApps;

namespace CrestApps.Mvc.Web.Areas.AIChat.Services;

public sealed class YesSqlAIChatSessionPromptStore : IAIChatSessionPromptStore
{
    private readonly ISession _session;

    public YesSqlAIChatSessionPromptStore(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId)
    {
        var prompts = await _session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(x => x.SessionId == sessionId).ListAsync();

        return prompts.OrderBy(p => p.CreatedUtc).ToArray();
    }

    public async Task<int> DeleteAllPromptsAsync(string sessionId)
    {
        var prompts = await _session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(x => x.SessionId == sessionId).ListAsync();
        var count = 0;

        foreach (var p in prompts)
        {
            _session.Delete(p);
            count++;
        }

        return count;
    }

    public async Task<int> CountAsync(string sessionId)
    {
        return await _session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(x => x.SessionId == sessionId).CountAsync();
    }

    public async ValueTask<AIChatSessionPrompt> FindByIdAsync(string id)
    {
        return await _session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(x => x.ItemId == id).FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<AIChatSessionPrompt>> GetAsync(IEnumerable<string> ids)
    {
        var items = await _session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(x => x.ItemId.IsIn(ids)).ListAsync();

        return items.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<AIChatSessionPrompt>> GetAllAsync()
    {
        var items = await _session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>().ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<AIChatSessionPrompt>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>();
        var skip = (page - 1) * pageSize;

        return new PageResult<AIChatSessionPrompt>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask CreateAsync(AIChatSessionPrompt record)
    {
        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(record);
    }

    public async ValueTask UpdateAsync(AIChatSessionPrompt record)
    {
        await _session.SaveAsync(record);
    }

    public ValueTask<bool> DeleteAsync(AIChatSessionPrompt entry)
    {
        _session.Delete(entry);

        return ValueTask.FromResult(true);
    }

    public async ValueTask SaveChangesAsync()
    {
        await _session.SaveChangesAsync();
    }
}
