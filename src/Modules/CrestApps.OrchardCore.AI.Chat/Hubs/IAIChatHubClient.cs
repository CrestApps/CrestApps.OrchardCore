namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public interface IAIChatHubClient
{
    Task ReceiveError(string error);

    Task LoadSession(object data);
}
