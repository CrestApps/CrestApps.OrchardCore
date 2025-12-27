namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public interface IChatInteractionHubClient
{
    Task ReceiveError(string error);

    Task LoadInteraction(object data);

    Task SettingsSaved(string itemId, string title);
}
