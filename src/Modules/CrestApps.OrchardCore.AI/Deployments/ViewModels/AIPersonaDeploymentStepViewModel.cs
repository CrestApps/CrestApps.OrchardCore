namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

/// <summary>
/// Backs the editor of the AI personas deployment step.
/// </summary>
public class AIPersonaDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether every saved persona is exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the saved personas to choose from.
    /// </summary>
    public AIPersonaEntryViewModel[] Personas { get; set; }
}

/// <summary>
/// A saved persona that can be exported.
/// </summary>
public class AIPersonaEntryViewModel
{
    /// <summary>
    /// Gets or sets the persona's item id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the text shown for the persona.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the persona is exported.
    /// </summary>
    public bool IsSelected { get; set; }
}
