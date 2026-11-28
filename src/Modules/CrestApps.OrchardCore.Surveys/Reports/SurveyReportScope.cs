using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// What a survey report reads, worked out from its filter.
/// </summary>
internal sealed class SurveyReportScope
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the versions read, newest first.
    /// </summary>
    public IReadOnlyList<SurveyVersion> Versions { get; set; } = [];

    /// <summary>
    /// Gets or sets every version of the survey, by id.
    /// </summary>
    public IReadOnlyDictionary<string, SurveyVersion> AllVersions { get; set; } = new Dictionary<string, SurveyVersion>();

    /// <summary>
    /// Gets or sets the definition whose questions the report lists: the newest version read.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the filter of the answer aggregates (completed responses in the date range).
    /// </summary>
    public SurveyAnswerFilter AnswerFilter { get; set; } = new();

    /// <summary>
    /// Gets or sets the scope of the response queries (responses started in the date range).
    /// </summary>
    public SurveyResultsScope ResultsScope { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the survey is anonymous.
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the fewest responses a group may show: the survey's minimum, never below the site's floor, or 0 for
    /// an identified survey.
    /// </summary>
    public int MinimumGroupSize { get; set; }

    /// <summary>
    /// Gets or sets whether the user may see who responded.
    /// </summary>
    public bool CanSeeIdentity { get; set; }

    /// <summary>
    /// Gets whether a group of a size is too small to show.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns><see langword="true"/> when the group must be hidden.</returns>
    public bool IsTooSmall(long size)
        => MinimumGroupSize > 0 && size < MinimumGroupSize;

    /// <summary>
    /// Gets the scope the minimum group size is counted on: the responses the narrowing filters (versions, dates,
    /// collector, context) select, completed and not tests, whatever status and test filters the report asks for, so
    /// previews and unfinished responses can't pad a group.
    /// </summary>
    /// <returns>A copy of <see cref="ResultsScope"/> for the threshold count.</returns>
    public SurveyResultsScope GroupSizeScope()
        => new()
        {
            SurveyId = ResultsScope.SurveyId,
            VersionNumbers = ResultsScope.VersionNumbers,
            FromUtc = ResultsScope.FromUtc,
            ToUtc = ResultsScope.ToUtc,
            CollectorId = ResultsScope.CollectorId,
            ContextKind = ResultsScope.ContextKind,
            IncludeTest = false,
            CompletedOnly = true,
            Statuses = [],
        };
}
