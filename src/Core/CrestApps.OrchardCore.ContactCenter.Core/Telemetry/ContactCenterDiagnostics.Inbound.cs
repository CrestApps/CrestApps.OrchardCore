using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The inbound funnel: routing an inbound call to an agent, a queue, a menu, an
/// announcement or an AI voice agent.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the span of routing one inbound call.
    /// </summary>
    internal const string InboundRouteSpanName = "crestapps.contactcenter.inbound.route";

    private const int MaxReasonCodeLength = 48;

    private static readonly Counter<long> _inboundRouted = Meter.CreateCounter<long>(
        "crestapps.contactcenter.inbound.routed",
        "{call}",
        "The number of inbound calls routed, by result (offered, queued, not_routed or duplicate) and the routing reason code.");

    private static readonly Histogram<double> _inboundRouteDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.inbound.route.duration",
        "The time spent routing an inbound call, by result and reason code.");

    private static readonly Histogram<double> _inboundAIAnswerDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.inbound.ai_answer.duration",
        "The time from when the provider received an inbound call to when routing handed it to the entry point's AI voice agent.");

    /// <summary>
    /// Starts measuring the routing of one inbound call.
    /// </summary>
    /// <param name="providerName">The provider the call came from.</param>
    /// <returns>The scope; finish it with <see cref="CompleteInboundRoute(TelemetryScope, InboundVoiceRoutingResult)"/>.</returns>
    internal static TelemetryScope StartInboundRoute(string providerName)
        => TelemetryScope.Start(
            ActivitySource,
            InboundRouteSpanName,
            _inboundRouteDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, GetProvider(providerName) },
            });

    /// <summary>
    /// Records what routing an inbound call came to, on its scope and on <c>crestapps.contactcenter.inbound.routed</c>.
    /// </summary>
    /// <param name="scope">The scope started by <see cref="StartInboundRoute(string)"/>.</param>
    /// <param name="result">The routing result.</param>
    /// <param name="duplicate">Whether the call was already tracked, as for a redelivered inbound webhook, so nothing was
    /// routed again.</param>
    internal static void CompleteInboundRoute(TelemetryScope scope, InboundVoiceRoutingResult result, bool duplicate = false)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var routeResult = result is null
            ? "not_routed"
            : duplicate
                ? "duplicate"
                : result.Routed
                ? "offered"
                : result.Queued
                    ? "queued"
                    : "not_routed";
        var reason = GetReasonCode(result?.ReasonCode);

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, routeResult);
        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        scope.SetSpanTag("crestapps.contactcenter.interaction.id", result?.InteractionId);
        scope.Succeed();

        if (!_inboundRouted.Enabled)
        {
            return;
        }

        Add(_inboundRouted, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, routeResult },
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
        });
    }

    /// <summary>
    /// Records how long an inbound call waited, from when the provider received it, before routing handed it to an
    /// AI voice agent.
    /// </summary>
    /// <param name="providerName">The provider the call came from.</param>
    /// <param name="elapsed">The time from the provider receiving the call to the hand-over.</param>
    internal static void RecordInboundAIAnswer(string providerName, TimeSpan elapsed)
    {
        if (!_inboundAIAnswerDuration.Enabled)
        {
            return;
        }

        Record(_inboundAIAnswerDuration, Math.Max(0, elapsed.TotalSeconds), new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, GetProvider(providerName) },
        });
    }

    /// <summary>
    /// Keeps a routing reason code bounded. The codes are constants of the routing code, but one read back from a stored
    /// interaction is checked rather than trusted.
    /// </summary>
    /// <param name="reasonCode">The reason code.</param>
    /// <returns>The code, <c>none</c>, or <c>other</c> for a value that is not a short snake_case code.</returns>
    internal static string GetReasonCode(string reasonCode)
    {
        if (string.IsNullOrEmpty(reasonCode))
        {
            return "none";
        }

        if (reasonCode.Length > MaxReasonCodeLength)
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        foreach (var character in reasonCode)
        {
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return CrestAppsOrchardCoreTelemetry.Other;
            }
        }

        return reasonCode;
    }
}
