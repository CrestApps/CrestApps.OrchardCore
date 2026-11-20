using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The course outline a learner sees, in the player and on the course page: the sections and items with the learner's
/// states and the items that are locked to them.
/// </summary>
public class LearnerOutlineViewModel
{
    public CourseOutline Outline { get; set; }

    public EnrollmentProgress Progress { get; set; }

    public IDictionary<string, string> KindIcons { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the item open in the player, or <see langword="null"/> on the course page.
    /// </summary>
    public string CurrentItemId { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may open the items: an enrolled learner or course staff.
    /// </summary>
    public bool CanOpen { get; set; }

    /// <summary>
    /// Gets or sets the items locked to the learner, with the decision that keeps each locked, or <see langword="null"/>
    /// when nobody's release rules were evaluated.
    /// </summary>
    public IDictionary<string, ReleaseDecision> LockedItems { get; set; }

    /// <summary>
    /// Gets or sets whether the outline is the player's navigation: its sections collapse, and text shown in place is
    /// left out, because the player shows it with the items in the content column.
    /// </summary>
    public bool Navigation { get; set; }
}
