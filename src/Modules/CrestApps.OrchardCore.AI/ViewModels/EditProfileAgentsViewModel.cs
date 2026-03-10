using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditProfileAgentsViewModel
{
    public ToolEntry[] Agents { get; set; }

    public int AlwaysAvailableAgentCount { get; set; }
}
