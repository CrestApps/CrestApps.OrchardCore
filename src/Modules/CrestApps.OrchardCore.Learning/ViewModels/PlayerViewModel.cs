using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The course player: the outline with the learner's states, the open item and its controls.
/// </summary>
public class PlayerViewModel
{
    public CourseOutline Outline { get; set; }

    public Enrollment Enrollment { get; set; }

    public EnrollmentProgress Progress { get; set; }

    public CourseOutlineEntry Current { get; set; }

    public CourseItemProgress CurrentProgress { get; set; }

    public IShape CurrentShape { get; set; }

    public CourseItemAccessReason Access { get; set; }

    public string LockedReason { get; set; }

    public DateTime? AvailableFromUtc { get; set; }

    public string PreviousItemId { get; set; }

    public string NextItemId { get; set; }

    public bool TracksProgress { get; set; }

    public IDictionary<string, string> LockedItems { get; set; } = new Dictionary<string, string>();

    public IDictionary<string, string> KindIcons { get; set; } = new Dictionary<string, string>();
}
