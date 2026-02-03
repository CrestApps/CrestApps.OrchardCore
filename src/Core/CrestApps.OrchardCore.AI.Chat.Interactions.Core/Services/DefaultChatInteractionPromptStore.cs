using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Default implementation of <see cref="IChatInteractionPromptStore"/> that stores prompts as separate documents.
/// </summary>
public sealed class DefaultChatInteractionPromptStore : DocumentCatalog<ChatInteractionPrompt, ChatInteractionPromptIndex>, IChatInteractionPromptStore
{
    private readonly IClock _clock;

    public DefaultChatInteractionPromptStore(
        ISession session,
        IClock clock)
        : base(session)
    {
        CollectionName = AIConstants.CollectionName;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<ChatInteractionPrompt>> GetPromptsAsync(string chatInteractionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(chatInteractionId);

        var prompts = await Session.Query<ChatInteractionPrompt, ChatInteractionPromptIndex>(
            x => x.ChatInteractionId == chatInteractionId,
            collection: CollectionName)
            .OrderBy(x => x.CreatedUtc)
            .ThenBy(x => x.Id)
            .ListAsync();

        return prompts.ToArray();
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllPromptsAsync(string chatInteractionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(chatInteractionId);

        var prompts = await Session.Query<ChatInteractionPrompt, ChatInteractionPromptIndex>(
            x => x.ChatInteractionId == chatInteractionId,
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
    protected override ValueTask SavingAsync(ChatInteractionPrompt record)
    {
        // Ensure CreatedUtc is set when creating a new prompt
        if (record.CreatedUtc == default)
        {
            record.CreatedUtc = _clock.UtcNow;
        }

        return ValueTask.CompletedTask;
    }
}
