namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The codes Learning services return when they refuse a change or keep an item locked
/// (<see cref="ProgressUpdate.RefusedReason"/>, <see cref="ReleaseDecision.Reason"/> and
/// <see cref="CourseItemAccess.LockedReason"/>). Services never return text for people: the module that shows a refusal
/// turns its code into localized text. A custom release rule may return its own code; it is shown as is when no text is
/// known for it.
/// </summary>
public static class LearningRefusals
{
    /// <summary>
    /// A reason is required.
    /// </summary>
    public const string ReasonRequired = "ReasonRequired";

    /// <summary>
    /// Only an active enrollment can be approved.
    /// </summary>
    public const string NotActive = "NotActive";

    /// <summary>
    /// The course is not available.
    /// </summary>
    public const string CourseUnavailable = "CourseUnavailable";

    /// <summary>
    /// The course item was not found.
    /// </summary>
    public const string ItemNotFound = "ItemNotFound";

    /// <summary>
    /// You do not have access to this course.
    /// </summary>
    public const string NoAccess = "NoAccess";

    /// <summary>
    /// Someone else is changing this enrollment. Try again.
    /// </summary>
    public const string Busy = "Busy";

    /// <summary>
    /// This item is not available yet.
    /// </summary>
    public const string NotAvailableYet = "NotAvailableYet";

    /// <summary>
    /// This item is not available.
    /// </summary>
    public const string NotAvailable = "NotAvailable";

    /// <summary>
    /// This item opens later.
    /// </summary>
    public const string OpensLater = "OpensLater";

    /// <summary>
    /// The course has not started yet: its items open on its start date.
    /// </summary>
    public const string CourseNotStarted = "CourseNotStarted";

    /// <summary>
    /// This item is not part of the course outline.
    /// </summary>
    public const string NotInOutline = "NotInOutline";

    /// <summary>
    /// This item is shown in place and takes no progress.
    /// </summary>
    public const string NotTracked = "NotTracked";

    /// <summary>
    /// Complete the earlier item first.
    /// </summary>
    public const string CompleteEarlierItem = "CompleteEarlierItem";

    /// <summary>
    /// Reach the required score on the earlier item first.
    /// </summary>
    public const string ReachEarlierScore = "ReachEarlierScore";

    /// <summary>
    /// This item is not completed by marking it done.
    /// </summary>
    public const string NotMarkedDone = "NotMarkedDone";

    /// <summary>
    /// This item has no statement to confirm.
    /// </summary>
    public const string NoStatement = "NoStatement";

    /// <summary>
    /// Tick the statement to confirm it.
    /// </summary>
    public const string ConfirmStatement = "ConfirmStatement";
}
