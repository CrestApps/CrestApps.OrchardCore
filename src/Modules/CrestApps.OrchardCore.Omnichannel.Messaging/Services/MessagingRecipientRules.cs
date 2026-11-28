using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// The address a new message or a broadcast leaves from and the recipients it goes to, checked the way the composer
/// and the broadcast page check them, for those pages and the AI tools that share the rules.
/// </summary>
internal static class MessagingRecipientRules
{
    /// <summary>
    /// Resolves the sending address and its channel, normalizes the recipients for the channel, and finds the
    /// recipients that are not valid addresses, the typed addresses no contact owns that fail the stricter rule for
    /// typed addresses, and the recipients whose existing conversation the user may not send on.
    /// </summary>
    /// <param name="endpointManager">The manager the sending address is read from.</param>
    /// <param name="channelResolver">The resolver of the enabled messaging channels.</param>
    /// <param name="contactSearch">The contact search that holds typed addresses to the stricter rule.</param>
    /// <param name="workspaceBuilder">The workspace builder that tells which existing conversations the user may send on.</param>
    /// <param name="user">The user sending.</param>
    /// <param name="endpointId">The identifier of the sending address.</param>
    /// <param name="recipientsText">The recipients as typed, separated as the composer separates them.</param>
    /// <param name="contactAddresses">The recipients picked from the contacts.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the checks.</returns>
    public static async Task<MessagingRecipientCheck> CheckAsync(
        IOmnichannelChannelEndpointManager endpointManager,
        IMessagingChannelResolver channelResolver,
        MessagingContactSearch contactSearch,
        MessagingWorkspaceBuilder workspaceBuilder,
        ClaimsPrincipal user,
        string endpointId,
        string recipientsText,
        IEnumerable<string> contactAddresses,
        CancellationToken cancellationToken)
    {
        var endpoint = string.IsNullOrEmpty(endpointId) ? null : await endpointManager.FindByIdAsync(endpointId, cancellationToken);
        // An address can be used on several channels; the composer sends on the first messaging channel it has.
        var channel = endpoint is null ? null : channelResolver.GetAll().FirstOrDefault(candidate => endpoint.HasCapability(candidate.Name));

        var addresses = MessagingRecipients.Parse(recipientsText)
            .Concat(contactAddresses ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .ToList();

        var recipients = addresses
            .Select(address => channel?.NormalizeAddress(address) ?? address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var check = new MessagingRecipientCheck
        {
            Endpoint = endpoint,
            Channel = channel,
            Recipients = recipients,
        };

        if (endpoint is null || channel is null)
        {
            return check;
        }

        check.InvalidAddresses = recipients.Where(address => !channel.IsValidAddress(address)).ToArray();

        if (check.InvalidAddresses.Count == 0)
        {
            // An address no contact owns is held to the stricter rule for typed addresses, so a number is never
            // guessed into a country.
            check.UnlistedAddresses = await contactSearch.FindInvalidNewAddressesAsync(addresses, channel.Name, cancellationToken);
        }

        // A recipient who already has a conversation is written to in that conversation, so the sender is held to the
        // same rule as replying in it.
        if (recipients.Count > 0)
        {
            check.UnreachableRecipients = await workspaceBuilder.FindUnreachableRecipientsAsync(user, channel.Name, recipients, cancellationToken);
        }

        return check;
    }
}

/// <summary>
/// The outcome of <see cref="MessagingRecipientRules.CheckAsync"/>.
/// </summary>
internal sealed class MessagingRecipientCheck
{
    /// <summary>
    /// Gets the sending address, or <see langword="null"/> when none was found.
    /// </summary>
    public OmnichannelChannelEndpoint Endpoint { get; init; }

    /// <summary>
    /// Gets the messaging channel the address sends on, or <see langword="null"/> when it is used on none.
    /// </summary>
    public IMessagingChannel Channel { get; init; }

    /// <summary>
    /// Gets the recipients, normalized for the channel and without duplicates.
    /// </summary>
    public IReadOnlyList<string> Recipients { get; init; } = [];

    /// <summary>
    /// Gets the recipients that are not valid addresses on the channel.
    /// </summary>
    public IReadOnlyList<string> InvalidAddresses { get; set; } = [];

    /// <summary>
    /// Gets the typed addresses no contact owns that fail the stricter rule for typed addresses.
    /// </summary>
    public IReadOnlyList<string> UnlistedAddresses { get; set; } = [];

    /// <summary>
    /// Gets the recipients whose existing conversation the user may not send on.
    /// </summary>
    public IReadOnlyList<string> UnreachableRecipients { get; set; } = [];
}
