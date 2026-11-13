using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The filters of an enrollment list.
/// </summary>
public sealed class EnrollmentQuery
{
    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the courses the caller may see, when the caller's scope is limited. <see langword="null"/> means every course.
    /// </summary>
    public IReadOnlyCollection<string> CourseIds { get; set; }

    /// <summary>
    /// Gets or sets the learners the caller may see, when the caller's scope is limited. <see langword="null"/> means everyone.
    /// </summary>
    public IReadOnlyCollection<string> UserIds { get; set; }

    /// <summary>
    /// Gets or sets the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public EnrollmentStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets an enrollment number to find.
    /// </summary>
    public string Number { get; set; }
}
