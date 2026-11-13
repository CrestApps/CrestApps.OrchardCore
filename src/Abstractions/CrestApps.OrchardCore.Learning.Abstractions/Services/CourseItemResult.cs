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
}
