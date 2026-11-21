namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The kinds of <see cref="Models.LearningSecret"/> Learning itself keeps. Another module adds its own kind as an open
/// string, such as the access code of a quiz.
/// </summary>
public static class LearningSecretKinds
{
    /// <summary>
    /// The enrollment key of a course.
    /// </summary>
    public const string EnrollmentKey = "EnrollmentKey";
}
