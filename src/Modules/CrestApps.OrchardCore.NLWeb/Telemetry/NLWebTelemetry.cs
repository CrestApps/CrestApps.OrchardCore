using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.NLWeb.Services;

namespace CrestApps.OrchardCore.NLWeb.Telemetry;

/// <summary>
/// The meter of NLWeb, <c>CrestApps.OrchardCore.NLWeb</c>, and its instruments.
/// </summary>
public static class NLWebTelemetry
{
    /// <summary>
    /// The name of the NLWeb meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".NLWeb";

    /// <summary>
    /// The tag that records the mode the caller asked for: <c>list</c>, <c>summarize</c>, <c>generate</c> or
    /// <c>other</c>.
    /// </summary>
    public const string ModeTag = "mode";

    /// <summary>
    /// The question was answered with results.
    /// </summary>
    public const string OutcomeAnswered = "answered";

    /// <summary>
    /// The question was answered, but nothing matched it.
    /// </summary>
    public const string OutcomeEmpty = "empty";

    /// <summary>
    /// The pipeline asked the caller for more details instead of answering.
    /// </summary>
    public const string OutcomeElicitation = "elicitation";

    /// <summary>
    /// The pipeline failed; the <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/> carries the protocol's error code.
    /// </summary>
    public const string OutcomeFailed = "failed";

    /// <summary>
    /// The meter of NLWeb.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("NLWeb");

    /// <summary>
    /// The duration of an NLWeb question until its results and text were ready, tagged with <see cref="ModeTag"/>, the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> and, for a failure, the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>. Its count is the number of questions.
    /// </summary>
    public static Histogram<double> AskDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.nlweb.ask.duration",
        "The duration of an NLWeb question until its answer was ready.");

    /// <summary>
    /// The number of results an NLWeb question returned, tagged with <see cref="ModeTag"/>.
    /// </summary>
    public static Histogram<long> AskResults { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.nlweb.ask.results",
        "{result}",
        "The number of results an NLWeb question returned.");

    /// <summary>
    /// Records a question the pipeline answered.
    /// </summary>
    /// <param name="context">The question.</param>
    public static void RecordAsk(NLWebAskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!AskDuration.Enabled && !AskResults.Enabled)
        {
            return;
        }

        var mode = GetMode(context.Mode);
        var tags = new TagList
        {
            { ModeTag, mode },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        var durationTags = tags;

        if (context.Failure is not null)
        {
            durationTags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, OutcomeFailed);
            durationTags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, GetErrorCode(context.Failure.Code));
        }
        else
        {
            durationTags.Add(
                CrestAppsOrchardCoreTelemetry.OutcomeTag,
                context.Elicitation is not null ? OutcomeElicitation : context.Results.Count > 0 ? OutcomeAnswered : OutcomeEmpty);
        }

        AskDuration.Record(context.Elapsed.TotalSeconds, durationTags);

        if (context.Failure is null && context.Elicitation is null)
        {
            AskResults.Record(context.Results.Count, tags);
        }
    }

    private static string GetMode(string mode)
    {
        return mode switch
        {
            NLWebProtocol.Modes.List => "list",
            NLWebProtocol.Modes.Summarize => "summarize",
            NLWebProtocol.Modes.Generate => "generate",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    private static string GetErrorCode(string code)
    {
        return code switch
        {
            NLWebProtocol.ErrorCodes.InvalidRequest => "invalid_request",
            NLWebProtocol.ErrorCodes.Forbidden => "forbidden",
            NLWebProtocol.ErrorCodes.NotConfigured => "not_configured",
            NLWebProtocol.ErrorCodes.UnknownPromise => "unknown_promise",
            NLWebProtocol.ErrorCodes.InternalError => "internal_error",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }
}
