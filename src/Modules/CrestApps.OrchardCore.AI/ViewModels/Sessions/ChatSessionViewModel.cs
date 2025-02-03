using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.ViewModels.Sessions;

public class ChatSessionViewModel
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public IShape Content { get; set; }

    public IList<IShape> History { get; set; }
}
