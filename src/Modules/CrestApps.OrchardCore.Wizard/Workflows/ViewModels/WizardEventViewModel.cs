namespace CrestApps.OrchardCore.Wizard.Workflows.ViewModels;

/// <summary>
/// Represents the editor view model shared by the wizard workflow events.
/// </summary>
public class WizardEventViewModel
{
    /// <summary>
    /// Gets or sets an optional wizard type this event is limited to.
    /// </summary>
    public string WizardType { get; set; }
}
