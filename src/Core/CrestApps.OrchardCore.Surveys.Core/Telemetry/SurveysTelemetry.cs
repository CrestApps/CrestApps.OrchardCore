using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Surveys.Core.Telemetry;

/// <summary>
/// The meter and activity source of Surveys and their instruments. Only Surveys' own operations are measured.
/// </summary>
public static class SurveysTelemetry
{
    /// <summary>
    /// The name of the meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Surveys";

    /// <summary>
    /// The tag that holds the survey kind.
    /// </summary>
    public const string KindTag = "kind";

    /// <summary>
    /// The tag that holds where a page post went: next, back or finish.
    /// </summary>
    public const string DirectionTag = CrestAppsOrchardCoreTelemetry.DirectionTag;

    /// <summary>
    /// The kind recorded when a survey has none.
    /// </summary>
    public const string UnknownKind = CrestAppsOrchardCoreTelemetry.Unknown;

    /// <summary>
    /// Gets the meter.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Surveys");

    /// <summary>
    /// Gets the activity source.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Surveys");

    /// <summary>
    /// Gets the number of responses started.
    /// </summary>
    public static Counter<long> ResponsesStarted { get; } = Meter.CreateCounter<long>(
        "crestapps.surveys.responses.started",
        "{response}",
        "The number of survey responses started.");

    /// <summary>
    /// Gets the number of responses completed.
    /// </summary>
    public static Counter<long> ResponsesCompleted { get; } = Meter.CreateCounter<long>(
        "crestapps.surveys.responses.completed",
        "{response}",
        "The number of survey responses completed.");

    /// <summary>
    /// Gets the number of respondents screened out.
    /// </summary>
    public static Counter<long> ResponsesScreenedOut { get; } = Meter.CreateCounter<long>(
        "crestapps.surveys.responses.screened_out",
        "{response}",
        "The number of survey respondents screened out.");

    /// <summary>
    /// Gets the number of responses abandoned.
    /// </summary>
    public static Counter<long> ResponsesAbandoned { get; } = Meter.CreateCounter<long>(
        "crestapps.surveys.responses.abandoned",
        "{response}",
        "The number of unfinished survey responses abandoned.");

    /// <summary>
    /// Gets how long a page post takes on the server.
    /// </summary>
    public static Histogram<double> PagePostDuration { get; } = Meter.CreateHistogram<double>(
        "crestapps.surveys.page_post.duration",
        "s",
        "How long the server takes to handle a survey page post.");

    /// <summary>
    /// Gets how long scoring a response takes.
    /// </summary>
    public static Histogram<double> ScoringDuration { get; } = Meter.CreateHistogram<double>(
        "crestapps.surveys.scoring.duration",
        "s",
        "How long scoring a survey response takes.");
}
