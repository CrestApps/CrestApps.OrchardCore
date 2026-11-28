using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One branch in the page editor.
/// </summary>
public class SurveyBranchEditViewModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the rule, as JSON.
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// Gets or sets the optional formula.
    /// </summary>
    public string Formula { get; set; }

    /// <summary>
    /// Gets or sets whether it goes to a page or an ending.
    /// </summary>
    public BranchTargetKind TargetKind { get; set; }

    /// <summary>
    /// Gets or sets the page or ending it goes to.
    /// </summary>
    public string TargetId { get; set; }
}
