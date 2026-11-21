namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The order of a page of enrollments.
/// </summary>
public enum EnrollmentOrder
{
    /// <summary>
    /// The newest enrollment first.
    /// </summary>
    Newest,

    /// <summary>
    /// The soonest due date first.
    /// </summary>
    DueSoonest,

    /// <summary>
    /// The soonest end of access first.
    /// </summary>
    AccessEndsSoonest,
}
