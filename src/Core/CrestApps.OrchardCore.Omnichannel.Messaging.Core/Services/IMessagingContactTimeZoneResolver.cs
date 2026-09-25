using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Resolves the time zone a conversation's contact lives in, so quiet hours are evaluated where the customer is
/// rather than where the agent or the server is.
/// </summary>
public interface IMessagingContactTimeZoneResolver
{
    /// <summary>
    /// Resolves the contact's time zone identifier, or <see langword="null"/> when it is unknown.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> ResolveAsync(MessagingConversation conversation, CancellationToken cancellationToken = default);
}
