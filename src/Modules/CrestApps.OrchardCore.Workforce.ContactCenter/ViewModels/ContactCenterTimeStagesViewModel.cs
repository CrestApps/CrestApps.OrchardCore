using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;

/// <summary>
/// The stage of each agent state and reason.
/// </summary>
public class ContactCenterTimeStagesViewModel
{
    /// <summary>
    /// Gets or sets the states.
    /// </summary>
    public List<ContactCenterTimeStageRow> States { get; set; } = [];

    /// <summary>
    /// Gets or sets the reasons.
    /// </summary>
    public List<ContactCenterTimeStageRow> Reasons { get; set; } = [];

    /// <summary>
    /// Gets or sets the stages to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StageOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor, built by the display drivers of this view model, whose fields are grouped in cards by their placement.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}

/// <summary>
/// A state or reason and its stage.
/// </summary>
public class ContactCenterTimeStageRow
{
    /// <summary>
    /// Gets or sets the state's or reason's name, as it is kept.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets what it is called on the page.
    /// </summary>
    [BindNever]
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets a hint about it.
    /// </summary>
    [BindNever]
    public string Hint { get; set; }

    /// <summary>
    /// Gets or sets its stage.
    /// </summary>
    public string StageId { get; set; }
}
