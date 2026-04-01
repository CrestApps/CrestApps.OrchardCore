using CrestApps.OrchardCore.AI.Chat.Core.Hubs;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public interface IChatInteractionHubClient : IChatHubClient
{
    Task LoadInteraction(object data);
    Task SettingsSaved(string itemId, string title);
    Task HistoryCleared(string itemId);
}
