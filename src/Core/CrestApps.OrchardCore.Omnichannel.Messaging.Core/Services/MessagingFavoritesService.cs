using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingFavoritesService"/>: the list lives on the agent profile's property bag
/// (<see cref="MessagingFavorites"/>), beside the agent's messaging availability.
/// </summary>
public sealed class MessagingFavoritesService : IMessagingFavoritesService
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingFavoritesService"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile manager the list is saved with.</param>
    /// <param name="clock">The clock.</param>
    public MessagingFavoritesService(IAgentProfileManager agentProfileManager, IClock clock)
    {
        _agentProfileManager = agentProfileManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MessagingFavorite> GetFavorites(AgentProfile agent)
    {
        if (agent is null || !agent.TryGet<MessagingFavorites>(out var favorites) || favorites?.Items is null)
        {
            return [];
        }

        return favorites.Items.Where(item => item is not null).ToArray();
    }

    /// <inheritdoc/>
    public bool IsFavorite(AgentProfile agent, MessagingConversation conversation)
        => conversation is not null && GetFavorites(agent).Any(item => item.Matches(conversation));

    /// <inheritdoc/>
    public async Task<bool> SetFavoriteAsync(AgentProfile agent, MessagingConversation conversation, string displayName, bool favorite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(conversation);

        var items = GetFavorites(agent).ToList();
        var existing = items.Where(item => item.Matches(conversation)).ToArray();

        if (favorite)
        {
            if (existing.Length > 0)
            {
                return false;
            }

            items.Insert(0, new MessagingFavorite
            {
                CustomerKey = conversation.GetCustomerKey(),
                ContactContentItemId = conversation.ContactContentItemId,
                Channel = conversation.Channel,
                ContactAddress = conversation.ContactAddress,
                DisplayName = displayName,
                AddedUtc = _clock.UtcNow,
            });

            // The oldest star falls off, rather than refusing a new one the agent just asked for.
            if (items.Count > MessagingFavorites.MaxFavorites)
            {
                items.RemoveRange(MessagingFavorites.MaxFavorites, items.Count - MessagingFavorites.MaxFavorites);
            }
        }
        else
        {
            if (existing.Length == 0)
            {
                return false;
            }

            items.RemoveAll(item => existing.Contains(item));
        }

        agent.Put(new MessagingFavorites { Items = items });

        await _agentProfileManager.UpdateAsync(agent, cancellationToken: cancellationToken);

        return true;
    }
}
