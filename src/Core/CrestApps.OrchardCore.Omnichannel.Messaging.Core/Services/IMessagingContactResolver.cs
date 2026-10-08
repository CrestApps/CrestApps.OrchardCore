namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Resolves the Omnichannel contact content item behind a contact address on a channel, so a conversation links to
/// the CRM contact and the workspace can show the same customer's conversations on every channel together.
/// </summary>
public interface IMessagingContactResolver
{
    /// <summary>
    /// Resolves the content item id of the contact that owns the specified address on a channel.
    /// </summary>
    /// <param name="channel">The channel the address belongs to.</param>
    /// <param name="address">The contact's normalized address.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The contact content item id, or <see langword="null"/> when unknown.</returns>
    ValueTask<string> ResolveContactContentItemIdAsync(string channel, string address, CancellationToken cancellationToken = default);
}
