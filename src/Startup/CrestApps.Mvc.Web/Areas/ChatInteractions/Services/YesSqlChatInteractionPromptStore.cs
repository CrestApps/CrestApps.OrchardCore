using CrestApps.AI.Chat;
using CrestApps.AI.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Services;

public sealed class YesSqlChatInteractionPromptStore : IChatInteractionPromptStore
{
    private readonly ISession _session;

    public YesSqlChatInteractionPromptStore(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyCollection<ChatInteractionPrompt>> GetPromptsAsync(string chatInteractionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(chatInteractionId);

        var prompts = await _session
            .Query<ChatInteractionPrompt, ChatInteractionPromptIndex>(x => x.ChatInteractionId == chatInteractionId)
            .ListAsync();

        return prompts.OrderBy(p => p.CreatedUtc).ToArray();
    }

    public async Task<int> DeleteAllPromptsAsync(string chatInteractionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(chatInteractionId);

        var prompts = await _session
            .Query<ChatInteractionPrompt, ChatInteractionPromptIndex>(x => x.ChatInteractionId == chatInteractionId)
            .ListAsync();

        var count = 0;

        foreach (var prompt in prompts)
        {
            _session.Delete(prompt);
            count++;
        }

        return count;
    }

    public async ValueTask<ChatInteractionPrompt> FindByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return await _session
            .Query<ChatInteractionPrompt, ChatInteractionPromptIndex>(x => x.ItemId == id)
            .FirstOrDefaultAsync();
    }

    public async ValueTask<IReadOnlyCollection<ChatInteractionPrompt>> GetAsync(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var items = await _session
            .Query<ChatInteractionPrompt, ChatInteractionPromptIndex>(x => x.ItemId.IsIn(ids))
            .ListAsync();

        return items.ToArray();
    }

    public async ValueTask<IReadOnlyCollection<ChatInteractionPrompt>> GetAllAsync()
    {
        var items = await _session
            .Query<ChatInteractionPrompt, ChatInteractionPromptIndex>()
            .ListAsync();

        return items.ToArray();
    }

    public async ValueTask<PageResult<ChatInteractionPrompt>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var query = _session.Query<ChatInteractionPrompt, ChatInteractionPromptIndex>();
        var skip = (page - 1) * pageSize;

        return new PageResult<ChatInteractionPrompt>
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async ValueTask CreateAsync(ChatInteractionPrompt record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.ItemId))
        {
            record.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(record);
    }

    public async ValueTask UpdateAsync(ChatInteractionPrompt record)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _session.SaveAsync(record);
    }

    public ValueTask<bool> DeleteAsync(ChatInteractionPrompt entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _session.Delete(entry);

        return ValueTask.FromResult(true);
    }

    public async ValueTask SaveChangesAsync()
    {
        await _session.SaveChangesAsync();
    }
}
