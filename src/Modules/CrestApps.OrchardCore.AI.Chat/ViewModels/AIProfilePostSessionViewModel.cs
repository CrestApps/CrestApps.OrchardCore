namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class AIProfilePostSessionViewModel
{
    public bool EnablePostSessionProcessing { get; set; }

    public List<PostSessionTaskViewModel> Tasks { get; set; } = [];

    public Dictionary<string, PostSessionToolEntry[]> PostSessionTools { get; set; } = [];
}

public class PostSessionToolEntry
{
    public string ItemId { get; set; }

    public string DisplayText { get; set; }

    public string Description { get; set; }

    public bool IsSelected { get; set; }
}
