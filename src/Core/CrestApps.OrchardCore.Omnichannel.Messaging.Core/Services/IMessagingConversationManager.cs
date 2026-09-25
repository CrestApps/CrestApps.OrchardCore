using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The management contract for <see cref="MessagingConversation"/>.
/// </summary>
public interface IMessagingConversationManager : ICatalogManager<MessagingConversation>
{
    /// <summary>
    /// Finds the conversation for a given address pair on a channel, loading its handlers.
    /// </summary>
    /// <param name="channel">The channel the thread runs on.</param>
    /// <param name="serviceAddress">Our normalized address the thread runs on.</param>
    /// <param name="contactAddress">The contact's normalized address.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching conversation, or <see langword="null"/> when none exists.</returns>
    Task<MessagingConversation> FindByAddressesAsync(string channel, string serviceAddress, string contactAddress, CancellationToken cancellationToken = default);
}
