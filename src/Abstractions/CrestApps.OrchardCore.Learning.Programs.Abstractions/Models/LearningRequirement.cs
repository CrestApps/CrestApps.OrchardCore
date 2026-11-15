using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Programs.Models;

/// <summary>
/// Required learning: a course, a learning path or a certification path required of an audience or of named people,
/// with a due date, reminders before it and escalation to the manager after it.
/// </summary>
public sealed class LearningRequirement : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is applied.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the kind of target, such as <c>Course</c>, <c>LearningPath</c> or <c>CertificationPath</c>.
    /// </summary>
    public string TargetKind { get; set; }

    /// <summary>
    /// Gets or sets the target's id.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the user audience required to complete it.
    /// </summary>
    public string AudienceName { get; set; }

    /// <summary>
    /// Gets or sets people required to complete it besides the audience.
    /// </summary>
    public List<string> UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how the due date is set.
    /// </summary>
    public RequiredLearningDueRule DueRule { get; set; }

    /// <summary>
    /// Gets or sets the due day, for <see cref="RequiredLearningDueRule.FixedDate"/>.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Gets or sets the number of days, for the rules that count days.
    /// </summary>
    public int DueDays { get; set; }

    /// <summary>
    /// Gets or sets how many days before the due date reminders are sent, such as 14, 7 and 1.
    /// </summary>
    public List<int> ReminderDays { get; set; } = [];

    /// <summary>
    /// Gets or sets how many days after the due date the person's manager is told; <see langword="null"/> never.
    /// </summary>
    public int? EscalateAfterDays { get; set; }

    /// <summary>
    /// Gets or sets when the rule last ran.
    /// </summary>
    public DateTime? LastRunUtc { get; set; }

    /// <summary>
    /// Gets or sets how many people the rule covered when it last ran.
    /// </summary>
    public int LastRunCovered { get; set; }

    /// <summary>
    /// Gets or sets when the rule was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the rule was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// How required learning sets the due date.
/// </summary>
public enum RequiredLearningDueRule
{
    /// <summary>
    /// No due date.
    /// </summary>
    None,

    /// <summary>
    /// A fixed day.
    /// </summary>
    FixedDate,

    /// <summary>
    /// Some days after the learning is required of the person.
    /// </summary>
    DaysAfterRequired,

    /// <summary>
    /// Some days after the person's hire date, or after it is required when there is none.
    /// </summary>
    DaysAfterHire,
}
