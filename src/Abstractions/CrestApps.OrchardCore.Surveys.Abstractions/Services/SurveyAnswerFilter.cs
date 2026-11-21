namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Which completed answers an aggregate reads.
/// </summary>
public sealed class SurveyAnswerFilter
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the version numbers read, or empty for all versions (joined by question id).
    /// </summary>
    public List<int> VersionNumbers { get; set; } = [];

    /// <summary>
    /// Gets or sets the earliest completion time, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? CompletedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest completion time (exclusive), in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? CompletedToUtc { get; set; }

    /// <summary>
    /// Gets or sets the ids of the responses read, or <see langword="null"/> for every response that matches. A segment
    /// (responses that chose one choice of another question) is passed this way.
    /// </summary>
    public IReadOnlyCollection<string> ResponseIds { get; set; }

    /// <summary>
    /// Gets or sets the collector, or <see langword="null"/> for all.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the context kind, such as LearningEnrollment, or <see langword="null"/> for all.
    /// </summary>
    public string ContextKind { get; set; }

    /// <summary>
    /// Gets or sets the context, <c>Kind:Id</c>, or <see langword="null"/> for all.
    /// </summary>
    public string ContextRef { get; set; }
}
