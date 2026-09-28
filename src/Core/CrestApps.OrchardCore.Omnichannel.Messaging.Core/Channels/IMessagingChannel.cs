using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// One non-voice communication channel the messaging workspace can send and receive on: SMS today, and Email,
/// WhatsApp, Facebook Messenger and the like as further features. The workspace itself never knows what a
/// channel is. It keeps conversations, routes them, tracks who owns them and renders them the same way for every
/// channel, and asks the channel only for what genuinely differs: how an address is written, how a message
/// leaves, and how a contact is reached and opted out on it.
/// </summary>
/// <remarks>
/// <para>
/// A channel is registered with <c>AddMessagingChannel&lt;TChannel&gt;()</c> from the feature that provides it.
/// Its <see cref="Name"/> is the value carried on <c>OmnichannelMessage.Channel</c> and
/// <c>OmnichannelChannelEndpoint.Channel</c>, so the endpoints a tenant configures for the channel and the
/// messages it exchanges line up with its conversations without any mapping.
/// </para>
/// <para>
/// Inbound traffic is fed to the workspace by the channel's own receiver (a provider webhook, an event handler)
/// calling <c>IMessagingInboundProcessor.ProcessAsync</c> with a normalized <c>OmnichannelMessage</c>.
/// </para>
/// </remarks>
public interface IMessagingChannel
{
    /// <summary>
    /// Gets the channel's technical name, such as <c>SMS</c>. It is stored on every conversation, message and
    /// endpoint of the channel, so it must never change once the channel has been used.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown for the channel in the workspace.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the CSS classes of the icon that identifies the channel in the workspace, such as
    /// <c>fa-solid fa-comment-sms</c>.
    /// </summary>
    string IconCssClass { get; }

    /// <summary>
    /// Gets the order the channel is shown in, lowest first, wherever the workspace lists channels side by side.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Gets what the channel can carry, so the composer and the workspace services adapt to it rather than
    /// assuming a text message.
    /// </summary>
    MessagingChannelCapabilities Capabilities { get; }

    /// <summary>
    /// Brings an address into the canonical form the channel stores and matches on, for example E.164 for a
    /// phone number or lower case for an email address. Two spellings of one address must normalize to the same
    /// value, because the normalized form is the conversation key.
    /// </summary>
    /// <param name="address">The address as it was received or typed.</param>
    /// <returns>The normalized address, or <see langword="null"/> when <paramref name="address"/> is empty.</returns>
    string NormalizeAddress(string address);

    /// <summary>
    /// Formats a normalized address for display.
    /// </summary>
    /// <param name="address">The normalized address.</param>
    /// <returns>The address as a person would expect to read it.</returns>
    string FormatAddress(string address);

    /// <summary>
    /// Determines whether a typed address is one this channel can deliver to.
    /// </summary>
    /// <param name="address">The address to check.</param>
    /// <returns><see langword="true"/> when the address is usable on this channel.</returns>
    bool IsValidAddress(string address);

    /// <summary>
    /// Hands one outbound message to the provider that serves the sending endpoint.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider's answer, carrying its own message identifier when it reported one.</returns>
    Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the contact has asked not to be reached on this channel. The workspace refuses to send
    /// to an opted-out contact and sends them nothing automated.
    /// </summary>
    /// <param name="contact">The contact content item.</param>
    /// <returns><see langword="true"/> when the contact has opted out of this channel.</returns>
    bool IsOptedOut(ContentItem contact);

    /// <summary>
    /// Lists the addresses the contact can be reached at on this channel, preferred first.
    /// </summary>
    /// <param name="contact">The contact content item.</param>
    /// <returns>The contact's normalized addresses on this channel; empty when there are none.</returns>
    IReadOnlyList<string> GetContactAddresses(ContentItem contact);

    /// <summary>
    /// Finds the contacts that own the specified address on this channel.
    /// </summary>
    /// <param name="address">The normalized address.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The content item identifiers of the matching contacts; empty when the address is unknown.</returns>
    Task<IReadOnlyList<string>> FindContactIdsAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds contacts whose address on this channel contains the typed term, for the composer's contact search.
    /// Matching by name is done by the workspace for every channel, so this only needs to cover addresses.
    /// </summary>
    /// <param name="term">The term the user typed.</param>
    /// <param name="contactTypes">The content types that are contacts.</param>
    /// <param name="take">The most contacts to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The content item identifiers of the matching contacts.</returns>
    Task<IReadOnlyList<string>> SearchContactIdsByAddressAsync(
        string term,
        IReadOnlyCollection<string> contactTypes,
        int take,
        CancellationToken cancellationToken = default);
}
