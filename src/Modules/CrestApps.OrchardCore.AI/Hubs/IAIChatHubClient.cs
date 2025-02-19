namespace CrestApps.OrchardCore.AI.Hubs;

public interface IAIChatHubClient
{
    Task ReceiveError(string error);

    Task LoadSession(object data);

    Task StartMessageStream(string messageId);

    Task CompleteMessageStream(string messageId);

    Task ReceiveMessageStream(CompletionPartialMessage message, string messageId);
}
