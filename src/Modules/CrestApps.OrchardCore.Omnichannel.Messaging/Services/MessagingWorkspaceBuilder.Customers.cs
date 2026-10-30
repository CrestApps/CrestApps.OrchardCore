using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

// The customers the inbox lists outside its page of conversations: the open customer kept on an unread-only list,
// and the agent's favorites.
public sealed partial class MessagingWorkspaceBuilder
{
    // The open customer's conversations the user may view, on the filtered channel, as one inbox row.
    private async Task<InboxRowGrouping.CustomerGroup> FindSelectedGroupAsync(ClaimsPrincipal user, string customerKey, string channel, CancellationToken cancellationToken)
    {
        var candidates = (await _conversationStore.GetForCustomerAsync(customerKey, cancellationToken)) ?? [];
        var visible = new List<MessagingConversation>();

        foreach (var conversation in candidates.OrderByDescending(item => item.LastMessageUtc ?? item.CreatedUtc))
        {
            if ((channel is null || string.Equals(conversation.Channel, channel, StringComparison.OrdinalIgnoreCase)) &&
                await AuthorizeAsync(user, conversation, ConversationOperation.View))
            {
                visible.Add(conversation);
            }
        }

        var groups = InboxRowGrouping.Group(visible);

        return groups.Count > 0 ? groups[0] : null;
    }

    private async Task<MessagingConversation> FindFavoriteConversationAsync(ClaimsPrincipal user, MessagingFavorite favorite, CancellationToken cancellationToken)
    {
        var candidates = (await _conversationStore.GetForCustomerAsync(favorite.CustomerKey, cancellationToken))?.ToList() ?? [];

        // A customer starred by their number, who has since been linked to a contact, is found by the number.
        if (candidates.Count == 0 && !string.IsNullOrEmpty(favorite.Channel) && !string.IsNullOrEmpty(favorite.ContactAddress))
        {
            var byAddress = await _conversationStore.FindByContactAsync(favorite.Channel, favorite.ContactAddress, cancellationToken);

            if (byAddress is not null)
            {
                candidates.Add(byAddress);
            }
        }

        foreach (var conversation in candidates.OrderByDescending(item => item.LastMessageUtc ?? item.CreatedUtc))
        {
            if (await AuthorizeAsync(user, conversation, ConversationOperation.View))
            {
                return conversation;
            }
        }

        return null;
    }

    // The starred customers, named by their contact record as it reads now, and by the name they were starred under
    // when the record is gone.
    private async Task<IList<FavoriteViewModel>> BuildFavoritesAsync(IReadOnlyList<MessagingFavorite> favorites, string selectedCustomerKey)
    {
        if (favorites.Count == 0)
        {
            return [];
        }

        var contactIds = favorites
            .Select(favorite => favorite.ContactContentItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var contacts = contactIds.Length == 0
            ? new Dictionary<string, ContentItem>(StringComparer.Ordinal)
            : (await _contentManager.GetAsync(contactIds, VersionOptions.Latest))
                .Where(item => item is not null)
                .GroupBy(item => item.ContentItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var result = new List<FavoriteViewModel>();

        foreach (var favorite in favorites)
        {
            var channel = _channelResolver.Get(favorite.Channel);
            var addressDisplay = channel?.FormatAddress(favorite.ContactAddress) ?? favorite.ContactAddress;
            var contactName = !string.IsNullOrEmpty(favorite.ContactContentItemId) && contacts.TryGetValue(favorite.ContactContentItemId, out var contact)
                ? contact.DisplayText
                : null;

            result.Add(new FavoriteViewModel
            {
                Favorite = favorite,
                Name = !string.IsNullOrWhiteSpace(contactName)
                    ? contactName
                    : !string.IsNullOrWhiteSpace(favorite.DisplayName) ? favorite.DisplayName : addressDisplay,
                AddressDisplay = addressDisplay,
                ChannelIconCssClass = channel?.IconCssClass,
                IsSelected = !string.IsNullOrEmpty(selectedCustomerKey) &&
                    string.Equals(favorite.CustomerKey, selectedCustomerKey, StringComparison.Ordinal),
            });
        }

        return result;
    }
}
