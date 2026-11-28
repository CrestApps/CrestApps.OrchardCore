using System.Globalization;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The scope of a results summary and the aggregates an analyzer reads.
/// </summary>
public sealed class SurveyAnalysisContext
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the filter the aggregates apply.
    /// </summary>
    public SurveyAnswerFilter Filter { get; set; } = new();

    /// <summary>
    /// Gets or sets the aggregates over the answer index.
    /// </summary>
    public ISurveyAnswerAggregates Aggregates { get; set; }

    /// <summary>
    /// Gets or sets the culture texts are written in.
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Gets or sets the format context of answer texts.
    /// </summary>
    public SurveyFormatContext Format { get; set; } = new();

    /// <summary>
    /// Gets or sets the fewest responses a group may show; groups below it are hidden (anonymous surveys).
    /// </summary>
    public int MinimumGroupSize { get; set; }
}
