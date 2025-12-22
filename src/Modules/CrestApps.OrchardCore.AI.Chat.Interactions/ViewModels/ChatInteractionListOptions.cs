namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class ChatInteractionListOptions
{
    public string SearchText { get; set; }

    public IDictionary<string, string> RouteValues { get; set; } = new Dictionary<string, string>();
}
