namespace CrestApps.OrchardCore.Wizard.Models;

/// <summary>
/// The configurable settings for a <see cref="WizardPart"/> attachment on a content type. They restrict the
/// content types a step can be, control the authoring editor, and define how a started wizard behaves.
/// </summary>
public sealed class WizardPartSettings
{
    /// <summary>
    /// Gets or sets the content types allowed as wizard steps. An empty array allows any content type that
    /// matches <see cref="ContainedStereotypes"/>.
    /// </summary>
    public string[] ContainedContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the stereotypes allowed as wizard steps. An empty array does not filter by stereotype.
    /// </summary>
    public string[] ContainedStereotypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the display type used to render each contained step in the authoring editor.
    /// </summary>
    public string DisplayType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether contained steps are collapsed by default in the authoring
    /// editor.
    /// </summary>
    public bool CollapseContainedItems { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a visitor must be authenticated to start the wizard.
    /// </summary>
    public bool RequiresAuthenticatedUser { get; set; }

    /// <summary>
    /// Gets or sets the policy that determines what happens to the collected response content items when the
    /// wizard completes.
    /// </summary>
    public WizardCompletionPolicy CompletionPolicy { get; set; } = WizardCompletionPolicy.Publish;
}
