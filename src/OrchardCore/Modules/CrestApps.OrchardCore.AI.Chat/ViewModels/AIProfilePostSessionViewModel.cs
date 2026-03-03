namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class AIProfilePostSessionViewModel
{
    public bool EnablePostSessionProcessing { get; set; }

    public List<PostSessionTaskViewModel> Tasks { get; set; } = [];
}
