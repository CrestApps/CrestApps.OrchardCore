using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class PostSessionTaskViewModel
{
    public string Name { get; set; }

    public PostSessionTaskType Type { get; set; }

    public string Instructions { get; set; }

    public bool AllowMultipleValues { get; set; }

    public List<PostSessionTaskOptionViewModel> Options { get; set; } = [];
}

public class PostSessionTaskOptionViewModel
{
    public string Value { get; set; }

    public string Description { get; set; }
}
