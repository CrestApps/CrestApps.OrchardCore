using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Reads the analytics event of each AI chat session for the chat analytics reports.
/// </summary>
public interface IAIChatSessionEventReader
{
    /// <summary>
    /// Lists the events of the sessions that match a query, newest first.
    /// </summary>
    /// <param name="query">The sessions to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<AIChatSessionEvent>> ListAsync(AIChatSessionEventQuery query, CancellationToken cancellationToken = default);
}
