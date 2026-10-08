namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// The customers an agent has starred in the messaging workspace, the people they write to most. Kept on the agent
/// profile's property bag, so each agent has their own list and it needs no schema.
/// </summary>
public sealed class MessagingFavorites
{
    /// <summary>
    /// The most customers one agent can star, so the list stays a shortcut rather than a second inbox.
    /// </summary>
    public const int MaxFavorites = 100;

    /// <summary>
    /// Gets or sets the starred customers, most recently starred first.
    /// </summary>
    public IList<MessagingFavorite> Items { get; set; } = [];
}

/// <summary>
/// One starred customer. The address is kept alongside the customer key, so a customer starred before a contact
/// record was linked to their address is still recognised afterwards, and a new message can be started to them even
/// when they have no conversation yet.
/// </summary>
public sealed class MessagingFavorite
{
    /// <summary>
    /// Gets or sets the key that identifies the customer across channels (see
    /// <see cref="MessagingConversation.GetCustomerKey()"/>).
    /// </summary>
    public string CustomerKey { get; set; }

    /// <summary>
    /// Gets or sets the linked contact, when the customer has a contact record.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the channel the customer was starred on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the customer's normalized address on that channel.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets the name the customer was shown by when they were starred, used when the contact record is gone.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets when the customer was starred, in UTC.
    /// </summary>
    public DateTime AddedUtc { get; set; }

    /// <summary>
    /// Determines whether this favorite is the customer behind a conversation.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <returns><see langword="true"/> when the conversation belongs to this customer.</returns>
    public bool Matches(MessagingConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        return string.Equals(CustomerKey, conversation.GetCustomerKey(), StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(ContactContentItemId) &&
                string.Equals(ContactContentItemId, conversation.ContactContentItemId, StringComparison.Ordinal)) ||
            (string.Equals(Channel, conversation.Channel, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ContactAddress, conversation.ContactAddress, StringComparison.Ordinal));
    }
}
