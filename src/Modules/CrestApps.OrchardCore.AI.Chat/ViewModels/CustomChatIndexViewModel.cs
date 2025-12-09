using CrestApps.OrchardCore.AI.Chat.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class CustomChatIndexViewModel
{
    public IList<AICustomChatInstance> Instances { get; set; } = [];

    public string ActiveInstanceId { get; set; }

    public IShape SettingsEditor { get; set; }

    public IShape ChatEditor { get; set; }
}
