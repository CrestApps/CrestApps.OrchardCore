using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIChatSessionManager
{
    /// <summary>
    /// Asynchronously retrieves an existing AI chat session by its session ID.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the chat session. Must not be null or empty.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is the <see cref="AIChatSession"/> if found,
    /// or <c>null</c> if no session with the specified session ID exists.
    /// </returns>
    Task<AIChatSession> FindAsync(string sessionId);

    /// <summary>
    /// Asynchronously retrieves a list of top AI chat sessions based on the provided pagination parameters and query context.
    /// </summary>
    /// <param name="page">The page number to retrieve (1-based index). Must be greater than 0.</param>
    /// <param name="pageSize">The number of sessions to retrieve per page. Must be greater than 0.</param>
    /// <param name="context">The context used to filter and order the chat sessions. Must not be null.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is a list of <see cref="AIChatSessionResult"/> objects,
    /// which represent the top sessions based on the query context and pagination parameters.
    /// </returns>
    Task<AIChatSessionResult> PageAsync(int page, int pageSize, AIChatSessionQueryContext context);

    /// <summary>
    /// Asynchronously creates a new AI chat session for the specified AI chat profile.
    /// </summary>
    /// <param name="profile">The AI chat profile for which the new session will be created. Must not be null.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is a new <see cref="AIChatSession"/>
    /// associated with the provided profile.
    /// </returns>
    Task<AIChatSession> NewAsync(AIProfile profile);

    /// <summary>
    /// Asynchronously saves or updates the specified AI chat session.
    /// </summary>
    /// <param name="chatSession">The AI chat session to save or update. Must not be null.</param>
    /// <returns>
    /// A task representing the asynchronous operation. This method does not return any value.
    /// </returns>
    Task SaveAsync(AIChatSession chatSession);
}
