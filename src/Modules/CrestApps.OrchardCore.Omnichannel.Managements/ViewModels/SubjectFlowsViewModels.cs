using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// View model for the Subject Flows index page.
/// </summary>
public class SubjectFlowsIndexViewModel
{
    /// <summary>
    /// Gets or sets the list of subject content types with their flow configuration status.
    /// </summary>
    public List<SubjectFlowEntryViewModel> Subjects { get; set; } = [];
}

/// <summary>
/// View model for a single subject entry in the Subject Flows index page.
/// </summary>
public class SubjectFlowEntryViewModel
{
    /// <summary>
    /// Gets or sets the content type name.
    /// </summary>
    public string ContentTypeName { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether flow settings have been configured.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether subject actions have been defined.
    /// </summary>
    public bool HasActions { get; set; }
}

/// <summary>
/// View model for the Subject Flow configure page.
/// </summary>
public class SubjectFlowConfigureViewModel
{
    /// <summary>
    /// Gets or sets the subject content type name.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the subject display name.
    /// </summary>
    public string SubjectDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the flow settings editor shape.
    /// </summary>
    public IShape Editor { get; set; }
}

/// <summary>
/// View model for a single subject action entry.
/// </summary>
public class SubjectActionEntryViewModel
{
    /// <summary>
    /// Gets or sets the subject action model.
    /// </summary>
    public SubjectAction Model { get; set; }

    /// <summary>
    /// Gets or sets the disposition display text.
    /// </summary>
    public string DispositionDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the action type display name.
    /// </summary>
    public string ActionTypeDisplayName { get; set; }
}

/// <summary>
/// View model for editing a subject action.
/// </summary>
public class EditSubjectActionViewModel
{
    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the subject display name.
    /// </summary>
    public string SubjectDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the action type display name.
    /// </summary>
    public string ActionTypeDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the editor shape.
    /// </summary>
    public IShape Editor { get; set; }
}

/// <summary>
/// View model for the manage actions page.
/// </summary>
public class ManageSubjectActionsViewModel
{
    /// <summary>
    /// Gets or sets the subject content type name.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the subject display name.
    /// </summary>
    public string SubjectDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the list of subject actions.
    /// </summary>
    public List<SubjectActionEntryViewModel> Actions { get; set; } = [];

    /// <summary>
    /// Gets or sets the available action types.
    /// </summary>
    public IEnumerable<SubjectActionTypeEntry> ActionTypes { get; set; } = [];
}

