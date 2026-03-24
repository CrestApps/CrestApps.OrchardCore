using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Provides context to an <see cref="IChatResponseHandler"/> when processing a chat prompt.
/// Contains the user's prompt, connection details, session state, conversation history,
/// and references to the underlying session or interaction object.
/// </summary>
public sealed class ChatResponseHandlerContext
{
    /// <summary>
    /// Gets the user's prompt text.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets the SignalR connection ID of the client.
    /// This can be used by deferred handlers to send responses back to the client
    /// at a later time, or to target a SignalR group for reconnection support.
    /// </summary>
    public required string ConnectionId { get; init; }

    /// <summary>
    /// Gets the session identifier.
    /// For <see cref="ChatContextType.AIChatSession"/>, this is <see cref="AIChatSession.SessionId"/>.
    /// For <see cref="ChatContextType.ChatInteraction"/>, this is the <c>ChatInteraction.ItemId</c>.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the type of chat context.
    /// </summary>
    public required ChatContextType ChatType { get; init; }

    /// <summary>
    /// Gets the conversation history (prior messages in the session).
    /// </summary>
    public required IList<ChatMessage> ConversationHistory { get; init; }

    /// <summary>
    /// Gets the scoped service provider for resolving services.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Gets the AI profile associated with this session.
    /// Only set when <see cref="ChatType"/> is <see cref="ChatContextType.AIChatSession"/>.
    /// </summary>
    public AIProfile Profile { get; init; }

    /// <summary>
    /// Gets the chat session.
    /// Only set when <see cref="ChatType"/> is <see cref="ChatContextType.AIChatSession"/>.
    /// </summary>
    public AIChatSession ChatSession { get; init; }

    /// <summary>
    /// Gets the chat interaction.
    /// Only set when <see cref="ChatType"/> is <see cref="ChatContextType.ChatInteraction"/>.
    /// </summary>
    public ChatInteraction Interaction { get; init; }

    /// <summary>
    /// Gets or sets the assistant message appearance that should be applied to
    /// assistant messages streamed by the current handler invocation.
    /// </summary>
    public AssistantMessageAppearance AssistantAppearance { get; set; }

    /// <summary>
    /// Gets or sets an extensible property bag for passing additional data to handlers.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
