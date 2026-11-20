namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Something a cohort can be linked to so its people are enrolled, such as a course (Learning) or a learning path
/// (Learning Programs), named by the content part that marks it. The cohort sync calls it for each person who joins or
/// leaves a linked cohort, and for each member when a cohort is linked or unlinked. Register one with
/// <c>services.AddCohortLearningTarget&lt;T&gt;()</c> in a startup that requires the Learning Cohorts feature.
/// </summary>
public interface ICohortLearningTarget
{
    /// <summary>
    /// Gets the name of the content part that marks the items this target handles, such as <c>CoursePart</c>.
    /// </summary>
    string PartName { get; }

    /// <summary>
    /// Enrolls a person who is in a linked cohort. Doing it again for the same cohort changes nothing.
    /// </summary>
    /// <param name="targetId">The content item id of the course or path.</param>
    /// <param name="cohortId">The cohort the person is in.</param>
    /// <param name="userId">The person.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task JoinAsync(string targetId, string cohortId, string userId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends what <see cref="JoinAsync"/> did for a person who left a linked cohort, or whose cohort was unlinked. Only
    /// what the cohort sync made is ended: an enrollment that another source also grants, or that the person
    /// completed, stays.
    /// </summary>
    /// <param name="targetId">The content item id of the course or path.</param>
    /// <param name="cohortId">The cohort the person left.</param>
    /// <param name="userId">The person.</param>
    /// <param name="inAnotherLinkedCohort">Whether the person is still in another cohort linked to the same target.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task LeaveAsync(string targetId, string cohortId, string userId, bool inAnotherLinkedCohort, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// The id of the enrollment grant the cohort sync gives, one per cohort, course and person, so leaving one cohort ends
/// only its own grant.
/// </summary>
public static class CohortEnrollmentGrants
{
    /// <summary>
    /// Gets the source id of the grant a cohort gives a person in a course.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="userId">The person.</param>
    /// <returns>The source id.</returns>
    public static string SourceId(string cohortId, string courseId, string userId)
        => $"{cohortId}|{courseId}|{userId}";
}
