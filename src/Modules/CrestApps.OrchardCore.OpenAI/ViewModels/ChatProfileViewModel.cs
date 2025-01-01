using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class ChatProfileViewModel
{
    public string DisplayName { get; set; }

    public IShape Editor { get; set; }
}
