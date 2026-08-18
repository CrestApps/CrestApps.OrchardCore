using System.Runtime.Serialization;
using CrestApps.OrchardCore.Wizard.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement.Display.Models;

namespace CrestApps.OrchardCore.Wizard.ViewModels;

/// <summary>
/// The view model used to render a <see cref="WizardPart"/> on the front end.
/// </summary>
public class WizardPartViewModel
{
    /// <summary>
    /// Gets or sets the wizard part being rendered.
    /// </summary>
    public WizardPart WizardPart { get; set; }

    /// <summary>
    /// Gets or sets the settings that control how the part is rendered.
    /// </summary>
    public WizardPartSettings Settings { get; set; }

    /// <summary>
    /// Gets the display context used to render each contained step.
    /// </summary>
    [IgnoreDataMember]
    [BindNever]
    public BuildPartDisplayContext BuildPartDisplayContext { get; set; }

    /// <summary>
    /// Gets the display type used to render each contained step.
    /// </summary>
    public string DisplayType => Settings?.DisplayType;
}
