using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class ListChatSessionsViewModel
{
    public string ProfileId { get; set; }

    public IEnumerable<AIChatSession> ChatSessions { get; set; }

    public IShape Pager { get; set; }

    public AIChatListOptions Options { get; set; }

    public IShape Header { get; set; }
}
