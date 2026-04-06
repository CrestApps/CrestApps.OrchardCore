using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Areas.ChatInteractions.Models;

public sealed class ChatInteractionSettings
{
    public ChatMode ChatMode { get; set; } = ChatMode.TextInput;

    public bool EnableUserMemory { get; set; } = true;
}
