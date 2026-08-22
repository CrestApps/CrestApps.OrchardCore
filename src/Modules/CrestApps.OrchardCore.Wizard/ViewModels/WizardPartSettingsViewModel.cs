using System.Collections.Specialized;
using CrestApps.OrchardCore.Wizard.Contents;
using CrestApps.OrchardCore.Wizard.Core.Models;

namespace CrestApps.OrchardCore.Wizard.ViewModels;

/// <summary>
/// The view model used to edit the settings of a <see cref="WizardPart"/> attachment.
/// </summary>
public class WizardPartSettingsViewModel
{
    /// <summary>
    /// Gets or sets the current settings of the part attachment.
    /// </summary>
    public WizardPartSettings WizardPartSettings { get; set; }

    /// <summary>
    /// Gets or sets the display names of every content type, keyed by technical name.
    /// </summary>
    public NameValueCollection ContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the display type used to render each step in the authoring editor.
    /// </summary>
    public string DisplayType { get; set; }

    /// <summary>
    /// Gets or sets the content types allowed as steps.
    /// </summary>
    public string[] ContainedContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets how the allowed step content types are resolved.
    /// </summary>
    public WizardPartSettingType Source { get; set; }

    /// <summary>
    /// Gets or sets the comma-separated stereotypes allowed as steps.
    /// </summary>
    public string Stereotypes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether contained steps are collapsed by default in the editor.
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
    public WizardCompletionPolicy CompletionPolicy { get; set; }
}
