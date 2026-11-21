using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// How Learning ties surveys and their responses to its records: course items own surveys, and responses belong to an
/// enrollment (or, for anonymous course surveys, only to the course).
/// </summary>
public static class LearningSurveyContexts
{
    /// <summary>
    /// The context kind of a response given by a learner through an enrollment.
    /// </summary>
    public const string EnrollmentKind = "LearningEnrollment";

    /// <summary>
    /// The context kind of an anonymous response to a course survey: it names the course and nobody in it.
    /// </summary>
    public const string CourseKind = "LearningCourse";

    /// <summary>
    /// The response property that holds the id of the course.
    /// </summary>
    public const string CourseIdProperty = "CourseId";

    /// <summary>
    /// The response property that holds the id of the enrollment (identified responses only).
    /// </summary>
    public const string EnrollmentIdProperty = "EnrollmentId";

    /// <summary>
    /// The response property that holds the id of the course item.
    /// </summary>
    public const string CourseItemIdProperty = "CourseItemId";

    /// <summary>
    /// Gets the owner of the survey of a course item.
    /// </summary>
    /// <param name="contentItemId">The id of the course item.</param>
    /// <returns>The owner.</returns>
    public static RecordRef Owner(string contentItemId)
        => new(RecordKinds.ContentItem, contentItemId);

    /// <summary>
    /// Gets the context of a response given through an enrollment.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <returns>The context.</returns>
    public static RecordRef Enrollment(string enrollmentId)
        => new(EnrollmentKind, enrollmentId);

    /// <summary>
    /// Gets the context of an anonymous response to a course survey.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <returns>The context.</returns>
    public static RecordRef Course(string courseId)
        => new(CourseKind, courseId);

    /// <summary>
    /// Gets whether a survey owner is a content item, so it may be a course item.
    /// </summary>
    /// <param name="owner">The owner, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is a content item.</returns>
    public static bool IsContentOwner(RecordRef? owner)
        => owner is { } value && !value.IsEmpty && string.Equals(value.Kind, RecordKinds.ContentItem, StringComparison.Ordinal);
}
