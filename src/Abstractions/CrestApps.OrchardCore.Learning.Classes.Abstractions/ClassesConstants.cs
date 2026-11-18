namespace CrestApps.OrchardCore.Learning.Classes;

/// <summary>
/// The names Learning classes uses.
/// </summary>
public static class ClassesConstants
{
    /// <summary>
    /// The feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Learning.Classes";

    /// <summary>
    /// The course item kind of a class.
    /// </summary>
    public const string ClassKind = "Class";

    /// <summary>
    /// The source of a class's dated sessions in Scheduling; the source id is the class course item.
    /// </summary>
    public const string SessionSource = "Learning.Class";

    /// <summary>
    /// The source of a learner's registration in a session; the source id is the enrollment and the class item,
    /// joined by a colon (see <see cref="RegistrationSourceId"/>).
    /// </summary>
    public const string RegistrationSource = "Learning.Class";

    /// <summary>
    /// The notification type that tells a learner the class session they had a place in was cancelled.
    /// </summary>
    public const string SessionCancelledNotificationType = "LearningClassSessionCancelled";

    /// <summary>
    /// The notification types of class notices to learners, besides <see cref="SessionCancelledNotificationType"/>.
    /// </summary>
    public static class NotificationTypes
    {
        /// <summary>
        /// Staff, or a workflow, gave the learner a place in a class session.
        /// </summary>
        public const string Registered = "LearningClassRegistered";

        /// <summary>
        /// A place freed up in a full session the learner waits for: it is held for them, or free to take.
        /// </summary>
        public const string PlaceOffered = "LearningClassPlaceOffered";
    }

    /// <summary>
    /// The content types Learning classes adds.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// A class in a course outline.
        /// </summary>
        public const string Class = "LearningClass";
    }

    /// <summary>
    /// Gets the source id of a learner's registration for a class item.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="classItemId">The class course item.</param>
    /// <returns>The source id.</returns>
    public static string RegistrationSourceId(string enrollmentId, string classItemId)
        => $"{enrollmentId}:{classItemId}";

    /// <summary>
    /// Reads the enrollment and the class item from a registration's source id.
    /// </summary>
    /// <param name="sourceId">The source id.</param>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="classItemId">The class course item.</param>
    /// <returns><see langword="true"/> when the source id has both.</returns>
    public static bool TryReadRegistrationSourceId(string sourceId, out string enrollmentId, out string classItemId)
    {
        var separator = sourceId?.IndexOf(':', StringComparison.Ordinal) ?? -1;

        enrollmentId = separator > 0 ? sourceId[..separator] : null;
        classItemId = separator > 0 && separator < sourceId.Length - 1 ? sourceId[(separator + 1)..] : null;

        return enrollmentId is not null && classItemId is not null;
    }
}
