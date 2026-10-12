namespace CrestApps.OrchardCore.Wizard.ViewModels;

/// <summary>
/// The navigation model rendered with the default wizard stepper chrome.
/// </summary>
public class WizardFlowNavigation
{
    /// <summary>
    /// Gets or sets the wizard session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the wizard type discriminator.
    /// </summary>
    public string WizardType { get; set; }

    /// <summary>
    /// Gets or sets the optional identifier of the definition the wizard was started from.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the key of the previous step, or <see langword="null"/> when on the first step.
    /// </summary>
    public string PreviousStep { get; set; }

    /// <summary>
    /// Gets or sets the key of the current step.
    /// </summary>
    public string CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the key of the next step, or <see langword="null"/> when on the last step.
    /// </summary>
    public string NextStep { get; set; }
}
