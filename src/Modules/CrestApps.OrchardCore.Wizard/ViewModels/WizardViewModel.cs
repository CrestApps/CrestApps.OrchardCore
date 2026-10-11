using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Wizard.ViewModels;

/// <summary>
/// The view model that renders a single wizard step in the public stepper.
/// </summary>
public class WizardViewModel
{
    /// <summary>
    /// Gets or sets the wizard type discriminator.
    /// </summary>
    public string WizardType { get; set; }

    /// <summary>
    /// Gets or sets the optional identifier of the definition the wizard was started from.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the wizard session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the key of the step being rendered.
    /// </summary>
    public string Step { get; set; }

    /// <summary>
    /// Gets or sets the built step content shape.
    /// </summary>
    public IShape Content { get; set; }
}
