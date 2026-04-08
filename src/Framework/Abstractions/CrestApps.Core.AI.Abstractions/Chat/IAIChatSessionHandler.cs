using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.Chat;

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
