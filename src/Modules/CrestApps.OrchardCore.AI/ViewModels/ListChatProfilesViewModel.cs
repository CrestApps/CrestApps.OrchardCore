using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class ListChatProfilesViewModel
{
    public IList<AIChatProfileEntry> Profiles { get; set; }

    public AIChatProfileOptions Options { get; set; }

    public IEnumerable<string> SourceNames { get; set; }

    public IShape Pager { get; set; }
}
