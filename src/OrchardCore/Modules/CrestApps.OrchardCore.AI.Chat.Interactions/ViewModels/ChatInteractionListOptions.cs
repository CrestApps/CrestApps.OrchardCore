using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class ChatInteractionListOptions
{
    public string SearchText { get; set; }

    public RouteValueDictionary RouteValues { get; set; } = [];
}
