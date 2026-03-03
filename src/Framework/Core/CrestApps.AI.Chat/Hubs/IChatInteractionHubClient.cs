namespace CrestApps.AI.Chat.Hubs;

public interface IChatInteractionHubClient
{
    Task ReceiveError(string error);

    Task LoadInteraction(object data);

    Task SettingsSaved(string itemId, string title);

    Task HistoryCleared(string itemId);
}
