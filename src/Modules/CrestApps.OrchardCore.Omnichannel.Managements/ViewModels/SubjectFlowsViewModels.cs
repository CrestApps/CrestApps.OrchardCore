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

    /// <summary>
    /// Gets or sets a value indicating whether the current user can edit content type definitions, which
    /// enables the shortcut to edit the subject part settings.
    /// </summary>
    public bool CanEditContentTypes { get; set; }
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
    /// Gets or sets the primary communication direction configured for the subject.
    /// </summary>
    public SubjectDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the inbound interaction type configured for the subject.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the inbound channel configured for the subject.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a disposition is required to complete activities for the subject.
    /// </summary>
    public bool RequireDisposition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether subject actions have been defined.
    /// </summary>
    public bool HasActions { get; set; }
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
