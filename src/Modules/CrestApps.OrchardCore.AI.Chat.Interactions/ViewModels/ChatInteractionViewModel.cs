using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class ChatInteractionViewModel
{
    public string InteractionId { get; set; }

    public IShape Content { get; set; }

    public IList<IShape> History { get; set; }

    public IEnumerable<string> Sources { get; set; } = [];
}
