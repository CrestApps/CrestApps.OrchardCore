using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class ListChatSessionsViewModel
{
    public string ProfileId { get; set; }

    public IList<AIChatSession> ChatSessions { get; set; }

    public IShape Pager { get; set; }

    public AIChatListOptions Options { get; set; }

    public IShape Header { get; set; }
}
