using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.Core.Data.YesSql.Services;
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
    : base(session, AIConstants.AICollectionName)
    {
        _clock = clock;
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var prompts = await Session.Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(
            x => x.SessionId == sessionId,
            collection: CollectionName)
                .ListAsync();

        return prompts
            .OrderBy(prompt => prompt.CreatedUtc)
            .ThenBy(prompt => prompt.ItemId)
            .ToArray();
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
