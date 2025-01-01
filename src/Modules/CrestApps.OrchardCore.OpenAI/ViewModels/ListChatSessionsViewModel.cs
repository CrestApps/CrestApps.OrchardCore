using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class ListChatSessionsViewModel
{
    public string ProfileId { get; set; }

    public IList<OpenAIChatSession> ChatSessions { get; set; }

    public IShape Pager { get; set; }

    public OpenAIChatListOptions Options { get; set; }

    public IShape Header { get; set; }
}
