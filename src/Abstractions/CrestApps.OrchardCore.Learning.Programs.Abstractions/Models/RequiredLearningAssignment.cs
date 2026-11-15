using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Programs.Models;

/// <summary>
/// The state of one person under one required learning rule.
/// </summary>
public sealed class RequiredLearningAssignment : CatalogItem
{
    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public string RequirementId { get; set; }

    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public RequiredLearningState State { get; set; }

    /// <summary>
    /// Gets or sets when the learning was required of the person.
    /// </summary>
    public DateTime RequiredUtc { get; set; }

    /// <summary>
    /// Gets or sets when it is due: the end of the due day in the site's time zone.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the person completed it.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the reminders sent, as days before the due date.
    /// </summary>
    public List<int> RemindersSent { get; set; } = [];

    /// <summary>
    /// Gets or sets when the manager was told it is overdue.
    /// </summary>
    public DateTime? EscalatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the exemption, when one was granted.
    /// </summary>
    public RequiredLearningExemption Exemption { get; set; }

    /// <summary>
    /// Gets or sets when the person left the rule's audience before completing it.
    /// </summary>
    public DateTime? RemovedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the state was last worked out.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets whether the person still has to do something.
    /// </summary>
    public bool IsOpen => State is RequiredLearningState.Required or RequiredLearningState.InProgress or RequiredLearningState.Overdue;
}

/// <summary>
/// An exemption from required learning, such as recognition of prior learning.
/// </summary>
public sealed class RequiredLearningExemption
{
    /// <summary>
    /// Gets or sets why it was granted.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets who granted it.
    /// </summary>
    public string GrantedBy { get; set; }

    /// <summary>
    /// Gets or sets when.
    /// </summary>
    public DateTime GrantedUtc { get; set; }

    /// <summary>
    /// Gets or sets the protected file that holds the evidence, if any.
    /// </summary>
    public string EvidenceFileId { get; set; }

    /// <summary>
    /// Gets or sets the evidence file's name.
    /// </summary>
    public string EvidenceFileName { get; set; }
}

/// <summary>
/// The state of a person under required learning.
/// </summary>
public enum RequiredLearningState
{
    /// <summary>
    /// Required, not started.
    /// </summary>
    Required,

    /// <summary>
    /// Started.
    /// </summary>
    InProgress,

    /// <summary>
    /// Completed.
    /// </summary>
    Completed,

    /// <summary>
    /// Past the due date and not completed.
    /// </summary>
    Overdue,

    /// <summary>
    /// Exempted by a person with the permission.
    /// </summary>
    Exempt,

    /// <summary>
    /// No longer required: the person left the audience before completing it.
    /// </summary>
    NotRequired,
}
