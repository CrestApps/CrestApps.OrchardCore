using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class ListProfilesViewModel
{
    public IList<AIProfileEntry> Profiles { get; set; }

    public AIProfileOptions Options { get; set; }

    public IEnumerable<string> SourceNames { get; set; }

    public IShape Pager { get; set; }
}
