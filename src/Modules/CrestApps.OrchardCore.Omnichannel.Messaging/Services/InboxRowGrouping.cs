using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Folds a page of conversations into one entry per customer, because the workspace lists customers rather than
/// threads: a customer who wrote on two channels is one person waiting, not two.
/// </summary>
internal static class InboxRowGrouping
{
    /// <summary>
    /// Groups the conversations by customer, keeping the order of each customer's most recent conversation.
    /// </summary>
    /// <param name="conversations">The page of conversations, most recent first.</param>
    /// <returns>One group per customer, led by their most recent conversation.</returns>
    public static IReadOnlyList<CustomerGroup> Group(IEnumerable<MessagingConversation> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        var groups = new List<CustomerGroup>();
        var byKey = new Dictionary<string, CustomerGroup>(StringComparer.Ordinal);

        foreach (var conversation in conversations)
        {
            var key = conversation.GetCustomerKey();

            if (!byKey.TryGetValue(key, out var group))
            {
                group = new CustomerGroup(key, conversation);
                byKey[key] = group;
                groups.Add(group);
            }

            group.Conversations.Add(conversation);
        }

        return groups;
    }

    /// <summary>
    /// One customer's conversations on a page.
    /// </summary>
    internal sealed class CustomerGroup
    {
        public CustomerGroup(string customerKey, MessagingConversation latest)
        {
            CustomerKey = customerKey;
            Latest = latest;
        }

        /// <summary>
        /// Gets the customer key.
        /// </summary>
        public string CustomerKey { get; }

        /// <summary>
        /// Gets the customer's most recent conversation, which the row opens.
        /// </summary>
        public MessagingConversation Latest { get; }

        /// <summary>
        /// Gets every conversation of the customer on the page.
        /// </summary>
        public List<MessagingConversation> Conversations { get; } = [];

        /// <summary>
        /// Gets the unread messages waiting across the customer's conversations.
        /// </summary>
        public int UnreadCount => Conversations.Sum(conversation => Math.Max(0, conversation.UnreadCount));

        /// <summary>
        /// Gets the channels the customer has conversations on, in the order they were last active.
        /// </summary>
        public IReadOnlyList<string> Channels => Conversations
            .Select(conversation => conversation.Channel)
            .Where(channel => !string.IsNullOrEmpty(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
