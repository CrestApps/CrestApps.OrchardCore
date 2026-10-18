using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
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
    public List<SubjectFlowEntry> Subjects { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows the <c>admin-list</c> tag renders, in the layout (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
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
    public List<SubjectActionEntry> Actions { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows the <c>admin-list</c> tag renders, in the layout (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the available action types.
    /// </summary>
    public IEnumerable<SubjectActionTypeEntry> ActionTypes { get; set; } = [];
}
