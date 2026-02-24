using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class PostSessionTaskViewModel
{
    public string Name { get; set; }

    public PostSessionTaskType Type { get; set; }

    public string Instructions { get; set; }

    public string Options { get; set; }

    public bool IsRequired { get; set; }
}
