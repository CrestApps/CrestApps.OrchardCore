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
    /// Gets or sets the fewest responses a group may show, or 0 when there is no limit.
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
        => IsAnonymous && MinimumGroupSize > 0 && size < MinimumGroupSize;
}
