using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// An enrollment with its progress, grants and history.
/// </summary>
public class EnrollmentDetailViewModel
{
    public Enrollment Enrollment { get; set; }

    public string LearnerName { get; set; }

    public string CourseTitle { get; set; }

    public CourseOutline Outline { get; set; }

    public EnrollmentProgress Progress { get; set; }

    public IDictionary<string, string> ActorNames { get; set; } = new Dictionary<string, string>();

    public IDictionary<string, string> SourceNames { get; set; } = new Dictionary<string, string>();

    public bool CanManage { get; set; }

    public bool NeedsStaffApproval { get; set; }
}
