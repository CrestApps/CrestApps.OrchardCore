using System.Diagnostics;
using CrestApps.OrchardCore.Surveys.Core.Telemetry;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Telemetry;

/// <summary>
/// Counts responses started, completed, screened out and abandoned, tagged by survey kind. Test responses are left
/// out.
/// </summary>
internal sealed class SurveysTelemetryObserver : ISurveyResponseObserver
{
    /// <inheritdoc />
    public Task StartedAsync(SurveyResponseEventContext context)
        => Count(SurveysTelemetry.ResponsesStarted, context);

    /// <inheritdoc />
    public Task CompletedAsync(SurveyResponseEventContext context)
        => Count(SurveysTelemetry.ResponsesCompleted, context);

    /// <inheritdoc />
    public Task ScreenedOutAsync(SurveyResponseEventContext context)
        => Count(SurveysTelemetry.ResponsesScreenedOut, context);

    /// <inheritdoc />
    public Task AbandonedAsync(SurveyResponseEventContext context)
        => Count(SurveysTelemetry.ResponsesAbandoned, context);

    private static Task Count(System.Diagnostics.Metrics.Counter<long> counter, SurveyResponseEventContext context)
    {
        if (context?.Response is { IsTest: false })
        {
            counter.Add(1, new TagList { { SurveysTelemetry.KindTag, context.Survey?.Kind ?? SurveysTelemetry.UnknownKind } });
        }

        return Task.CompletedTask;
    }
}
