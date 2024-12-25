using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels.Sessions;

public class ChatSessionViewModel
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public IShape Content { get; set; }

    public IList<IShape> Messages { get; set; }
}
