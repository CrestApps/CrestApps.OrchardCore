namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Which responses of a survey a results query reads.
/// </summary>
public sealed class SurveyResultsScope
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the version numbers read, or empty for all.
    /// </summary>
    public List<int> VersionNumbers { get; set; } = [];

    /// <summary>
    /// Gets or sets the earliest start, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest start (exclusive), in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the collector, or <see langword="null"/>.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the kind of context, or <see langword="null"/>.
    /// </summary>
    public string ContextKind { get; set; }

    /// <summary>
    /// Gets or sets whether test responses are read.
    /// </summary>
    public bool IncludeTest { get; set; }

    /// <summary>
    /// Gets or sets whether only completed responses are read.
    /// </summary>
    public bool CompletedOnly { get; set; }
}
