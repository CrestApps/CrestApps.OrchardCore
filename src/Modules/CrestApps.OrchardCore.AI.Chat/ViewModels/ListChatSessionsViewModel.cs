using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class ListChatSessionsViewModel
{
    public string ProfileId { get; set; }

    public IEnumerable<AIChatSessionEntry> ChatSessions { get; set; }

    public IShape Pager { get; set; }

    public AIChatSessionListOptions Options { get; set; }

    public IShape Header { get; set; }
}
