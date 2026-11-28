using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

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

    /// <summary>
    /// Gets or sets the statuses whose non-test responses are left out, such as those of an anonymous survey too few
    /// responses share (see <see cref="Access.SurveyGroupGate"/>). Test responses of these statuses are still read.
    /// </summary>
    public List<SurveyResponseStatus> ExcludedStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the groups (collector, version, context kind) whose non-test responses are left out, such as the
    /// groups of an anonymous survey too small to show (see <see cref="Access.SurveyAnonymousGroups.HiddenGroupsAsync"/>).
    /// </summary>
    public List<SurveyResponseGroup> HiddenGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets whether documents are read in the order of their random ids rather than the order they were
    /// stored in, which follows when they started: an anonymous survey's rows must not give away who answered first.
    /// </summary>
    public bool OrderById { get; set; }
}
