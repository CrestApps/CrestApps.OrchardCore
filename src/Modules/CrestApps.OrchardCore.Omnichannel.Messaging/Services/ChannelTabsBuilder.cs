using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Builds the row of channel tabs above a customer's conversation. Every enabled channel gets a tab, whether or not
/// the customer has written on it, so the agent sees at a glance where the customer can be reached and where they
/// are waiting: a tab leads to the customer's conversation on that channel, or to the composer when they have an
/// address on the channel but no conversation yet, and is disabled when they cannot be reached on it at all.
/// </summary>
internal static class ChannelTabsBuilder
{
    /// <summary>
    /// Builds the tabs.
    /// </summary>
    /// <param name="channels">The enabled channels, in display order.</param>
    /// <param name="current">The conversation on screen.</param>
    /// <param name="customerConversations">Every conversation of the customer, on every channel.</param>
    /// <param name="contactAddresses">The customer's addresses on each channel, keyed by channel name.</param>
    /// <param name="conversationUrl">Builds the link to a conversation.</param>
    /// <param name="startUrl">Builds the link that starts a conversation on a channel with an address.</param>
    /// <param name="S">The string localizer used for the tooltips.</param>
    /// <returns>One tab per channel.</returns>
    public static IReadOnlyList<ChannelTabViewModel> Build(
        IReadOnlyList<IMessagingChannel> channels,
        MessagingConversation current,
        IReadOnlyList<MessagingConversation> customerConversations,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contactAddresses,
        Func<MessagingConversation, string> conversationUrl,
        Func<string, string, string> startUrl,
        IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(current);

        var tabs = new List<ChannelTabViewModel>(channels.Count);
        var conversations = customerConversations ?? [];

        foreach (var channel in channels)
        {
            var onChannel = conversations
                .Where(conversation => string.Equals(conversation.Channel, channel.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var isActive = string.Equals(current.Channel, channel.Name, StringComparison.OrdinalIgnoreCase);

            // The conversation on screen is the one its own tab shows; on any other channel it is the most recent
            // one, since the customer's last word there is what the agent would pick up.
            var target = isActive
                ? current
                : onChannel.OrderByDescending(conversation => conversation.LastMessageUtc ?? conversation.CreatedUtc).FirstOrDefault();

            // The thread on screen has just been read; everything else still waiting on the channel counts.
            var unread = onChannel
                .Where(conversation => !string.Equals(conversation.ItemId, current.ItemId, StringComparison.Ordinal))
                .Sum(conversation => Math.Max(0, conversation.UnreadCount));

            var tab = new ChannelTabViewModel
            {
                Name = channel.Name,
                DisplayName = channel.DisplayName.Value,
                IconCssClass = channel.IconCssClass,
                IsActive = isActive,
                HasConversation = target is not null,
                UnreadCount = unread,
            };

            if (target is not null)
            {
                tab.Url = conversationUrl(target);
                tab.Tooltip = channel.DisplayName.Value;
            }
            else if (contactAddresses is not null &&
                contactAddresses.TryGetValue(channel.Name, out var addresses) &&
                addresses is { Count: > 0 })
            {
                tab.Url = startUrl(channel.Name, addresses[0]);
                tab.Tooltip = S["Start a {0} conversation", channel.DisplayName.Value].Value;
            }
            else
            {
                tab.Tooltip = S["This customer has no {0} address on file.", channel.DisplayName.Value].Value;
            }

            tabs.Add(tab);
        }

        return tabs;
    }
}
