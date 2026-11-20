namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// The names of the release rules and completion criteria Learning registers. They are stored in
/// <c>LearningRuleSettings.Name</c> and in recipes, so they never change.
/// </summary>
public static class LearningRuleNames
{
    /// <summary>
    /// Release rule: another course item completed, optionally with a minimum score. Values: <c>ItemId</c>, <c>MinimumScore</c>.
    /// </summary>
    public const string ItemCompleted = "ItemCompleted";

    /// <summary>
    /// Release rule: a date reached. Values: <c>FromUtc</c>, an ISO 8601 UTC instant.
    /// </summary>
    public const string Date = "Date";

    /// <summary>
    /// Release rule: a number of days after the enrollment was created. Values: <c>Days</c>.
    /// </summary>
    public const string DaysAfterEnrollment = "DaysAfterEnrollment";

    /// <summary>
    /// Release rule: open only to named people or to the members of an audience. Values: <c>UserIds</c>, user ids
    /// separated by commas, and <c>Audience</c>, the name of a user audience.
    /// </summary>
    public const string People = "People";

    /// <summary>
    /// Completion criterion: a minimum course grade. Values: <c>Grade</c>, a percentage.
    /// </summary>
    public const string MinimumGrade = "MinimumGrade";

    /// <summary>
    /// Completion criterion: a minimum total time on the course items. Values: <c>Minutes</c>.
    /// </summary>
    public const string MinimumTime = "MinimumTime";

    /// <summary>
    /// Completion criterion: a named course item passed. Values: <c>ItemId</c>.
    /// </summary>
    public const string ItemPassed = "ItemPassed";

    /// <summary>
    /// Completion criterion: a member of the course staff approved the completion. Values: <c>Role</c>, optional, the
    /// course staff role whose members alone may approve, such as <c>Instructor</c>.
    /// </summary>
    public const string StaffApproval = "StaffApproval";

    /// <summary>
    /// The key in <c>Enrollment.Properties</c> that records a staff approval of the completion.
    /// </summary>
    public const string StaffApprovalProperty = "LearningStaffApproval";
}
