using System.Runtime.Serialization;
using CrestApps.OrchardCore.Wizard.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Wizard.ViewModels;

/// <summary>
/// The view model used to author the step-definition content items of a <see cref="WizardPart"/>.
/// </summary>
public class WizardPartEditViewModel
{
    /// <summary>
    /// Gets or sets the html field prefixes of the authored step items, in order.
    /// </summary>
    public string[] Prefixes { get; set; } = [];

    /// <summary>
    /// Gets or sets the content types of the authored step items, in order.
    /// </summary>
    public string[] ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the content item ids of the authored step items, in order.
    /// </summary>
    public string[] ContentItems { get; set; } = [];

    /// <summary>
    /// Gets or sets the wizard part being authored.
    /// </summary>
    [BindNever]
    public WizardPart WizardPart { get; set; }

    /// <summary>
    /// Gets or sets the updater used to build nested step editors.
    /// </summary>
    [IgnoreDataMember]
    [BindNever]
    public IUpdateModel Updater { get; set; }

    /// <summary>
    /// Gets or sets the content type definitions that a step may be.
    /// </summary>
    [BindNever]
    public IEnumerable<ContentTypeDefinition> ContainedContentTypeDefinitions { get; set; }

    /// <summary>
    /// Gets or sets the authored steps the current user may view or edit.
    /// </summary>
    [BindNever]
    public IEnumerable<WizardPartWidgetViewModel> AccessibleWidgets { get; set; }

    /// <summary>
    /// Gets or sets the type-part definition of the part being authored.
    /// </summary>
    [BindNever]
    public ContentTypePartDefinition TypePartDefinition { get; set; }
}
