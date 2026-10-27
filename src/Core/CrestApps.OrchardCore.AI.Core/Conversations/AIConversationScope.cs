using CrestApps.Core.AI.Orchestration;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Holds the <see cref="AIConversationContext"/> of the conversation the current code is running for.
/// </summary>
/// <remarks>
/// It flows with the async call, so a tool the model calls, a Liquid template rendered while the request is built,
/// and a workflow the tool starts all see the conversation without it being passed down. Scopes nest: disposing one
/// restores the context that was current before it.
/// </remarks>
public static class AIConversationScope
{
    private static readonly AsyncLocal<AIConversationContext> _current = new();

    /// <summary>
    /// Gets the context of the conversation the current code runs for, or <see langword="null"/> outside one.
    /// </summary>
    public static AIConversationContext Current => _current.Value;

    /// <summary>
    /// Gets the conversation the current code runs for: the one a channel made current, or else a chat on the
    /// website or in the admin, described from the AI session the chat is answering.
    /// </summary>
    /// <returns>The conversation, or <see langword="null"/> outside one.</returns>
    public static AIConversationContext Resolve()
    {
        if (_current.Value is not null)
        {
            return _current.Value;
        }

        var session = AIInvocationScope.Current?.ChatSession;

        return session is null
            ? null
            : new AIConversationContext
            {
                Channel = AIConversationChannels.Chat,
                Direction = AIConversationDirections.Inbound,
                Session = session,
            };
    }

    /// <summary>
    /// Makes <paramref name="context"/> current until the returned scope is disposed.
    /// </summary>
    /// <param name="context">The conversation's context.</param>
    /// <returns>The scope, which restores the previous context when disposed.</returns>
    public static Scope Begin(AIConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Scope(context, _current.Value);
    }

    /// <summary>
    /// A conversation scope; disposing it restores the context that was current before it began.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        private readonly AIConversationContext _previous;

        internal Scope(AIConversationContext context, AIConversationContext previous)
        {
            Context = context;
            _previous = previous;
            _current.Value = context;
        }

        /// <summary>
        /// Gets the context this scope made current.
        /// </summary>
        public AIConversationContext Context { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            _current.Value = _previous;
        }
    }
}
