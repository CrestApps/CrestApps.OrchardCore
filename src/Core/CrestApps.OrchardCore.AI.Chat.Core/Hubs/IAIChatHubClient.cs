using CrestApps.OrchardCore.AI.Chat.Core.Hubs;

namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public interface IAIChatHubClient : IChatHubClient
{
    Task LoadSession(object data);

    Task MessageRated(string messageId, bool? userRating);
}
