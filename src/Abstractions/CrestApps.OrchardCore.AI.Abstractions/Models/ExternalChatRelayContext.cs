namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Provides context for establishing a connection to an external chat relay.
/// Contains the session identity, chat type, and service provider for resolving dependencies.
/// </summary>
public sealed class ExternalChatRelayContext
{
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
    /// Gets the scoped service provider for resolving services.
    /// </summary>
    public required IServiceProvider Services { get; init; }
}
