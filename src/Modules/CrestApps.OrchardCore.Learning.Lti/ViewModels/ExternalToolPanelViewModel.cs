using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.ViewModels;

/// <summary>
/// The External tool panel of the learner player.
/// </summary>
public class ExternalToolPanelViewModel
{
    public string ItemId { get; set; }

    public string ToolName { get; set; }

    public bool SignedIn { get; set; }

    public bool Enrolled { get; set; }

    public bool CanLaunch { get; set; }

    public bool ToolAvailable { get; set; }

    public LtiScore Score { get; set; }
}
