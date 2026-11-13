namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// How a learner gets into a course on their own. People with the permission can always enroll a learner.
/// </summary>
public enum EnrollmentMode
{
    /// <summary>
    /// Only people with the permission, data pipelines, workflows and other sources enroll learners.
    /// </summary>
    Closed,

    /// <summary>
    /// A signed-in learner enrolls at once.
    /// </summary>
    Open,

    /// <summary>
    /// A signed-in learner enrolls with the course's enrollment key.
    /// </summary>
    Key,

    /// <summary>
    /// A signed-in learner asks to enroll and the enrollment waits for approval.
    /// </summary>
    Approval,
}
