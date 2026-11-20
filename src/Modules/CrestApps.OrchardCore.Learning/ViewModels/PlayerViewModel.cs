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

    public string BlockingItemId { get; set; }

    public string PreviousItemId { get; set; }

    public string NextItemId { get; set; }

    public bool TracksProgress { get; set; }

    public IDictionary<string, ReleaseDecision> LockedItems { get; set; } = new Dictionary<string, ReleaseDecision>();

    public IDictionary<string, string> KindIcons { get; set; } = new Dictionary<string, string>();

    public IList<CourseOutlineEntry> TextBefore { get; set; } = [];

    /// <summary>
    /// Gets or sets the text that ends the item's section, shown under the section's last item.
    /// </summary>
    public IList<CourseOutlineEntry> TextAfter { get; set; } = [];

    public IDictionary<string, string> KindNames { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the position of the item among the items learners step through, from 1.
    /// </summary>
    public int StepNumber { get; set; }

    public int StepCount { get; set; }

    /// <summary>
    /// Gets or sets how many items count toward completion.
    /// </summary>
    public int RequiredCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the items that count toward completion the learner completed.
    /// </summary>
    public int CompletedCount { get; set; }

    /// <summary>
    /// Gets or sets whether the player shows the course completion panel instead of an item.
    /// </summary>
    public bool ShowCompletion { get; set; }

    /// <summary>
    /// Gets or sets the links of the course completion panel, such as the certificate the completion earned.
    /// </summary>
    public IReadOnlyList<CourseCompletionLink> CompletionLinks { get; set; } = [];
}
