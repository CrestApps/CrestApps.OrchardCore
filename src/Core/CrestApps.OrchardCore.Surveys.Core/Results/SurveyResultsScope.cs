using CrestApps.OrchardCore.Surveys.Models;

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
    /// Gets or sets the start of the date range, in UTC, or <see langword="null"/>. A finished response (completed or
    /// screened out) matches on when it finished, an unfinished one on when it started, the same date the answer
    /// aggregates use.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the date range (exclusive), in UTC, or <see langword="null"/>.
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

    /// <summary>
    /// Gets or sets the statuses read, or empty for every status. <see cref="CompletedOnly"/> narrows further.
    /// </summary>
    public List<SurveyResponseStatus> Statuses { get; set; } = [];
}
