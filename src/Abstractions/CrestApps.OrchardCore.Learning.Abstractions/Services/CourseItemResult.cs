namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// A scored result of a course item, reported by the module that computed it.
/// </summary>
public sealed class CourseItemResult
{
    /// <summary>
    /// Gets or sets the content item id of the course item.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the score as a percentage.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets whether the result passed. <see langword="null"/> lets Learning compare the score with the item's pass mark.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="Passed"/> is the source's own decision even when it is <see langword="null"/>, as a
    /// quiz's is: its survey decides with its score bands and pass mark, and with neither it makes no pass decision.
    /// Learning then never falls back to the item's pass mark.
    /// </summary>
    public bool PassDecided { get; set; }

    /// <summary>
    /// Gets or sets whether the source makes no pass decision for work that is done, so the <c>Passed</c> completion mode
    /// counts the item as completed, neither passed nor failed: a quiz scored with neither a pass mark nor a deciding score
    /// band, or an anonymous survey, whose result is its completion only.
    /// </summary>
    public bool CompletedWithoutPassDecision { get; set; }

    /// <summary>
    /// Gets or sets whether the learner handed the work in, for the <c>Submitted</c> completion mode.
    /// </summary>
    public bool Submitted { get; set; }

    /// <summary>
    /// Gets or sets whether the work is graded, for the <c>Graded</c> completion mode.
    /// </summary>
    public bool Graded { get; set; }

    /// <summary>
    /// Gets or sets whether the learner attended, for the <c>Attended</c> completion mode.
    /// </summary>
    public bool Attended { get; set; }

    /// <summary>
    /// Gets or sets whether the browser reported the result, as a learning package does.
    /// </summary>
    public bool SelfReported { get; set; }

    /// <summary>
    /// Gets or sets whether a learning package reported the item complete.
    /// </summary>
    public bool ReportedComplete { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts so far.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets whether the result clears the score and pass of the item, such as when the only graded attempt of a
    /// quiz is voided. An item a staff member completed by hand stays completed.
    /// </summary>
    public bool ResetScore { get; set; }

    /// <summary>
    /// Gets or sets the user id of the member of the course staff who gave the result, such as a grader. A result with
    /// one is recorded on the manual path, as <see cref="ILearnerProgressService.CompleteItemAsync"/> is: the enrollment
    /// must be active, but the learner's access window and the item's release rules are not checked, because work handed
    /// in while the learner had access is graded later. <see langword="null"/> for a result of the learner's own work.
    /// </summary>
    public string RecordedBy { get; set; }

    /// <summary>
    /// Gets or sets, for a result of an anonymous response (such as an anonymous course survey), the start of the day it
    /// came in, in UTC. The progress then records the item's completion, and a course completion it causes, at that time
    /// instead of the current one, and the learner's last access and activity do not move, so no exact time links the
    /// learner to their anonymous answers. <see langword="null"/> records the current time.
    /// </summary>
    public DateTime? AnonymousDayUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the result is the item's completion only, with no score, as an anonymous survey's is. The item
    /// then stays out of the course grade (the other items are weighed without it) instead of counting as zero, and a
    /// score recorded on it earlier is cleared.
    /// </summary>
    public bool CompletionOnly { get; set; }
}
