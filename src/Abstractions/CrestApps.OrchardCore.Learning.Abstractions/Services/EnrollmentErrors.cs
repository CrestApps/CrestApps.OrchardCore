namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The reasons an enrollment request or change fails.
/// </summary>
public static class EnrollmentErrors
{
    /// <summary>
    /// The course does not exist or is not published.
    /// </summary>
    public const string CourseNotFound = "CourseNotFound";

    /// <summary>
    /// The user does not exist.
    /// </summary>
    public const string UserNotFound = "UserNotFound";

    /// <summary>
    /// The request is missing the learner, the course or the source.
    /// </summary>
    public const string Invalid = "Invalid";

    /// <summary>
    /// The course does not take self enrollments.
    /// </summary>
    public const string Closed = "Closed";

    /// <summary>
    /// The enrollment key is wrong.
    /// </summary>
    public const string InvalidKey = "InvalidKey";

    /// <summary>
    /// The learner does not hold the course's prerequisites.
    /// </summary>
    public const string PrerequisitesNotMet = "PrerequisitesNotMet";

    /// <summary>
    /// The enrollment does not exist.
    /// </summary>
    public const string NotFound = "NotFound";

    /// <summary>
    /// The enrollment cannot move to the requested state.
    /// </summary>
    public const string InvalidTransition = "InvalidTransition";

    /// <summary>
    /// The change needs a reason.
    /// </summary>
    public const string ReasonRequired = "ReasonRequired";

    /// <summary>
    /// Another request is changing the same enrollment; try again.
    /// </summary>
    public const string Busy = "Busy";
}
