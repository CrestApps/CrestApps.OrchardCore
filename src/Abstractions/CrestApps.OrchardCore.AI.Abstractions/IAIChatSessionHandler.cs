using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Handles lifecycle events raised during an AI chat session, such as when
/// a message exchange completes. Implementations can perform post-processing
/// tasks like data extraction, analytics, or workflow triggers.
/// Inherits from <see cref="ICatalogEntryHandler{T}"/> to support standard
/// lifecycle events (Initializing, Initialized, Creating, Created, Loaded,
/// Deleting, Deleted, Updating, Updated, Validating, Validated).
/// </summary>
public interface IAIChatSessionHandler : ICatalogEntryHandler<AIChatSession>
{
    /// <summary>
    /// Called after a user message has been processed and the assistant response
    /// has been fully generated and appended to the session.
    /// </summary>
    /// <param name="context">
    /// The context describing the completed message exchange, including the
    /// profile, session, messages, and an <see cref="IServiceProvider"/> scoped
    /// to the current request.
    /// </param>
    Task MessageCompletedAsync(ChatMessageCompletedContext context);
}

/// <summary>
/// Provides context for the <see cref="IAIChatSessionHandler.MessageCompletedAsync"/> callback.
/// </summary>
public sealed class ChatMessageCompletedContext
{
    /// <summary>
    /// Gets the AI profile that was used.
    /// </summary>
    public required AIProfile Profile { get; init; }

    /// <summary>
    /// Gets the current chat session.
    /// </summary>
    public required AIChatSession ChatSession { get; init; }

    /// <summary>
    /// Gets the prompts associated with the current chat session.
    /// </summary>
    public required IReadOnlyList<AIChatSessionPrompt> Prompts { get; init; }

    /// <summary>
    /// Gets or sets the time in milliseconds the AI took to generate the response.
    /// </summary>
    public double ResponseLatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the number of input tokens used in this completion.
    /// </summary>
    public int InputTokenCount { get; init; }

    /// <summary>
    /// Gets or sets the number of output tokens generated in this completion.
    /// </summary>
    public int OutputTokenCount { get; init; }
}
