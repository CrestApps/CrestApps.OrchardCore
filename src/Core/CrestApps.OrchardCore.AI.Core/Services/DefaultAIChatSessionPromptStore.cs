using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IAIChatSessionPromptStore"/> that stores prompts as separate documents.
/// </summary>
public sealed class DefaultAIChatSessionPromptStore : DocumentCatalog<AIChatSessionPrompt, AIChatSessionPromptIndex>, IAIChatSessionPromptStore
{
    private readonly IClock _clock;

    public DefaultAIChatSessionPromptStore(
        ISession session,
        IClock clock)
        : base(session)
    {
        CollectionName = AIConstants.CollectionName;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var prompts = await Session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(
            x => x.SessionId == sessionId,
            collection: CollectionName)
            .OrderBy(x => x.CreatedUtc)
            .ThenBy(x => x.Id)
            .ListAsync();

        return prompts.ToArray();
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllPromptsAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var prompts = await Session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(
            x => x.SessionId == sessionId,
            collection: CollectionName)
            .ListAsync();

        var count = 0;
        foreach (var prompt in prompts)
        {
            Session.Delete(prompt, CollectionName);
            count++;
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return await Session.QueryIndex<AIChatSessionPromptIndex>(
            x => x.SessionId == sessionId,
            collection: CollectionName)
            .CountAsync();
    }

    /// <inheritdoc />
    protected override ValueTask SavingAsync(AIChatSessionPrompt record)
    {
        if (record.CreatedUtc == default)
        {
            record.CreatedUtc = _clock.UtcNow;
        }

        return ValueTask.CompletedTask;
    }
}
