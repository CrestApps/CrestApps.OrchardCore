using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Reads, edits and rewrites what the AI remembers about a contact.
/// </summary>
public sealed class AIContactMemoryService
{
    private readonly IAIContactMemoryStore _store;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIContactMemoryService"/> class.
    /// </summary>
    /// <param name="store">The store the notes are kept in.</param>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="clock">The clock.</param>
    public AIContactMemoryService(
        IAIContactMemoryStore store,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IClock clock)
    {
        _store = store;
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _clock = clock;
    }

    /// <summary>
    /// Gets a contact's note, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="contactId">The contact's content item id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<string> GetAsync(string contactId, CancellationToken cancellationToken = default)
        => (await _store.FindByContactAsync(contactId, cancellationToken))?.Text;

    /// <summary>
    /// Replaces a contact's note, as a person edited it. An empty note clears it.
    /// </summary>
    /// <param name="contactId">The contact's content item id.</param>
    /// <param name="text">The note.</param>
    /// <param name="sessionId">The AI session the note came from, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task SetAsync(string contactId, string text, string sessionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(contactId);

        var memory = await _store.FindByContactAsync(contactId, cancellationToken);
        text = Clip(text);

        if (string.IsNullOrEmpty(text))
        {
            if (memory is not null)
            {
                await _store.DeleteAsync(memory, cancellationToken);
            }

            return;
        }

        memory ??= new AIContactMemory
        {
            ItemId = UniqueId.GenerateId(),
            ContactId = contactId,
        };

        memory.Text = text;
        memory.UpdatedUtc = _clock.UtcNow;
        memory.LastSessionId = sessionId;

        await _store.SaveAsync(memory, cancellationToken);
    }

    /// <summary>
    /// Rewrites a contact's note from a conversation: what is still true and still open from the note, plus what the
    /// conversation added, in at most <see cref="AIContactMemory.MaxLength"/> characters.
    /// </summary>
    /// <param name="contactId">The contact's content item id.</param>
    /// <param name="turns">The conversation.</param>
    /// <param name="sessionId">The AI session, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The new note, or <see langword="null"/> when nothing was rewritten.</returns>
    public async Task<string> RememberAsync(string contactId, IReadOnlyList<AIConversationTurn> turns, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contactId) || turns is null || !turns.Any(turn => turn.Role == AIConversationTurn.CustomerRole))
        {
            return null;
        }

        var deployment = await _deploymentManager.ResolveUtilityOrDefaultAsync();

        if (deployment is null)
        {
            return null;
        }

        var current = await GetAsync(contactId, cancellationToken);

        var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsageFeaturePurposes.ContactMemory));

        var response = await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, BuildRequest(current, turns)),
            ],
            cancellationToken: cancellationToken);

        var text = Clip(response.Text);

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        await SetAsync(contactId, text, sessionId, cancellationToken);

        return text;
    }

    /// <summary>
    /// Rewrites the note of the contact a finished conversation was with, when the profile saves contact memory.
    /// </summary>
    /// <param name="profile">The profile that held the conversation.</param>
    /// <param name="session">The conversation's AI session.</param>
    /// <param name="prompts">The session's prompts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The new note, or <see langword="null"/> when nothing was rewritten.</returns>
    public Task<string> RememberSessionAsync(AIProfile profile, AIChatSession session, IEnumerable<AIChatSessionPrompt> prompts, CancellationToken cancellationToken = default)
    {
        if (profile is null ||
            session is null ||
            !profile.GetSettings<AIProfileContactMemorySettings>().SaveMemory ||
            !session.TryGet<AIConversationContactMetadata>(out var contact))
        {
            return Task.FromResult<string>(null);
        }

        return RememberAsync(contact.ContactId, AIConversationTranscript.GetTurns(prompts), session.SessionId, cancellationToken);
    }

    /// <summary>
    /// Adds a contact's note to a system message.
    /// </summary>
    /// <param name="systemMessage">The system message.</param>
    /// <param name="memory">The note.</param>
    public static string AppendTo(string systemMessage, string memory)
        => string.IsNullOrWhiteSpace(memory)
            ? systemMessage
            : $"{systemMessage}{(string.IsNullOrEmpty(systemMessage) ? string.Empty : "\n\n")}What you remember about this customer from earlier conversations (use it naturally, do not read it out):\n{memory.Trim()}";

    internal static readonly string SystemPrompt =
        $"""
        You keep a short memory note about one customer of a business, so the business's assistant can pick up where
        the last conversation left off on any channel.
        Rewrite the note from the current note and the new conversation. Keep facts that are still true, preferences,
        and anything still open, such as a promised callback or an unresolved problem. Drop what was resolved or no
        longer matters. Never keep payment card numbers, passwords or other secrets.
        Write plain sentences or short bullet points, at most {AIContactMemory.MaxLength} characters, and nothing else.
        """;

    /// <summary>
    /// Builds the message the memory writer reads.
    /// </summary>
    /// <param name="current">The current note, or <see langword="null"/>.</param>
    /// <param name="turns">The conversation.</param>
    internal static string BuildRequest(string current, IEnumerable<AIConversationTurn> turns)
        => $"""
            Current note:
            {(string.IsNullOrWhiteSpace(current) ? "(none yet)" : current.Trim())}

            New conversation:
            {AIConversationTranscript.Format(turns)}
            """;

    /// <summary>
    /// Trims a note to <see cref="AIContactMemory.MaxLength"/> characters.
    /// </summary>
    /// <param name="text">The note.</param>
    internal static string Clip(string text)
    {
        text = text?.Trim();

        return string.IsNullOrEmpty(text) || text.Length <= AIContactMemory.MaxLength
            ? text
            : text[..AIContactMemory.MaxLength].TrimEnd();
    }
}
