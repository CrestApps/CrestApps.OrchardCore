using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Core.Hubs;

/// <summary>
/// The media-quality half of the soft phone hub: the browser's own measurements of the live peer connection,
/// rated and logged server-side. Split out from the hub so that adding to the call-quality contract -- which
/// grew a capture side once it turned out that every number here described only the direction the agent was
/// listening to -- does not mean growing an already long file.
/// </summary>
public abstract partial class TelephonyHubBase
{
    /// <summary>
    /// Receives a browser-measured media-quality sample (or an end-of-call summary) for the current user's
    /// call and logs it structured for observability and alerting. The server rates the report independently of
    /// the browser's own poor flag so alerting does not depend on a client-supplied value, and chooses the log
    /// severity from that rating so a poor connection surfaces as a warning without every periodic sample
    /// flooding the log.
    /// </summary>
    /// <param name="report">The measured media-quality report.</param>
    public async Task ReportCallQuality(CallQualityReport report)
    {
        if (report is null)
        {
            return;
        }

        await _scopeExecutor.ExecuteAsync(async services =>
        {
            if (!await AuthorizeAsync(services))
            {
                LogHubActionUnauthorized("ReportCallQuality");
                return;
            }

            var rating = TelephonyCallQualityEvaluator.Evaluate(report);

            if (rating == CallQualityRating.Poor)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Telephony call quality {Rating} for user {UserId}. CallId={CallId}, Mos={Mos:F2}, Loss={Loss:F1}%, Jitter={Jitter:F0}ms, Rtt={Rtt:F0}ms, Buffer={Buffer:F0}ms, Conceal={Conceal:F1}%, InLevel={InLevel:F3}, OutLevel={OutLevel:F3}, BytesReceived={Bytes}, Mic={Mic:F3}, MicReported={MicReported}, BytesSent={BytesSent}, Capture={CaptureRate}Hz {CaptureProcessing} on {CaptureDevice}, Sent={SentIsLocal}/{SentLabel}, SendCodec={SendCodec}, RttSource={RttSource}, RemoteLoss={RemoteLoss:F1}%, RemoteJitter={RemoteJitter:F0}ms, Echo={EchoReported}:{Erl:F1}/{Erle:F1}dB, Leg={ProviderLegId}, Ccid={ProviderCallControlId}, Codec={Codec}, Ice={LocalIce}/{RemoteIce}, Final={Final}.",
                        rating,
                        RedactedUserId(),
                        report.CallId.SanitizeLogValue(),
                        report.Mos,
                        report.LossPercent,
                        report.JitterMs,
                        report.RoundTripTimeMs,
                        report.JitterBufferMs,
                        report.ConcealmentPercent,
                        report.InboundLevel,
                        report.CaptureProbeLevel,
                        report.BytesReceived,
                        report.MicrophoneLevel,
                        report.CaptureReported,
                        report.BytesSent,
                        report.CaptureSampleRate,
                        report.CaptureProcessing.SanitizeLogValue(),
                        report.CaptureDevice.SanitizeLogValue(),
                        report.SentTrackReported ? (report.SentTrackIsLocalStream ? "local" : "OTHER") : "n/a",
                        report.SentTrackLabel.SanitizeLogValue(),
                        report.SendCodec.SanitizeLogValue(),
                        report.RttSource.SanitizeLogValue(),
                        report.RemoteFractionLostPercent,
                        report.RemoteJitterMs,
                        report.EchoStatsReported,
                        report.EchoReturnLossDb,
                        report.EchoReturnLossEnhancementDb,
                        report.ProviderLegId.SanitizeLogValue(),
                        report.ProviderCallControlId.SanitizeLogValue(),
                        report.Codec.SanitizeLogValue(),
                        report.LocalCandidateType.SanitizeLogValue(),
                        report.RemoteCandidateType.SanitizeLogValue(),
                        report.Final);
                }

                return;
            }

            if (report.Final)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Telephony call quality summary ({Rating}) for user {UserId}. CallId={CallId}, AvgMos={AvgMos:F2}, MinMos={MinMos:F2}, MaxLoss={MaxLoss:F1}%, Samples={Samples}, DurationMs={Duration}, Buffer={Buffer:F0}ms, Conceal={Conceal:F1}%, MaxBuffer={MaxBuffer:F0}ms, InLevel={InLevel:F3}, OutLevel={OutLevel:F3}, Mic={Mic:F3}, MinMic={MinMic:F3}, MicReported={MicReported}, BytesSent={BytesSent}, Capture={CaptureRate}Hz {CaptureProcessing} on {CaptureDevice}, Sent={SentIsLocal}/{SentLabel}, SendCodec={SendCodec}, RttSource={RttSource}, RemoteLoss={RemoteLoss:F1}%, RemoteJitter={RemoteJitter:F0}ms, Echo={EchoReported}:{Erl:F1}/{Erle:F1}dB, Leg={ProviderLegId}, Ccid={ProviderCallControlId}, Codec={Codec}, Ice={LocalIce}/{RemoteIce}.",
                        rating,
                        RedactedUserId(),
                        report.CallId.SanitizeLogValue(),
                        report.AvgMos,
                        report.MinMos,
                        report.MaxLossPercent,
                        report.SampleCount,
                        report.DurationMs,
                        report.JitterBufferMs,
                        report.ConcealmentPercent,
                        report.MaxJitterBufferMs,
                        report.InboundLevel,
                        report.CaptureProbeLevel,
                        report.MicrophoneLevel,
                        report.MinMicrophoneLevel,
                        report.CaptureReported,
                        report.BytesSent,
                        report.CaptureSampleRate,
                        report.CaptureProcessing.SanitizeLogValue(),
                        report.CaptureDevice.SanitizeLogValue(),
                        report.SentTrackReported ? (report.SentTrackIsLocalStream ? "local" : "OTHER") : "n/a",
                        report.SentTrackLabel.SanitizeLogValue(),
                        report.SendCodec.SanitizeLogValue(),
                        report.RttSource.SanitizeLogValue(),
                        report.RemoteFractionLostPercent,
                        report.RemoteJitterMs,
                        report.EchoStatsReported,
                        report.EchoReturnLossDb,
                        report.EchoReturnLossEnhancementDb,
                        report.ProviderLegId.SanitizeLogValue(),
                        report.ProviderCallControlId.SanitizeLogValue(),
                        report.Codec.SanitizeLogValue(),
                        report.LocalCandidateType.SanitizeLogValue(),
                        report.RemoteCandidateType.SanitizeLogValue());
                }

                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Telephony call quality sample ({Rating}) for user {UserId}. CallId={CallId}, Mos={Mos:F2}, Loss={Loss:F1}%, Jitter={Jitter:F0}ms, Rtt={Rtt:F0}ms, Buffer={Buffer:F0}ms, Conceal={Conceal:F1}%, InLevel={InLevel:F3}, OutLevel={OutLevel:F3}, BytesReceived={Bytes}, Mic={Mic:F3}, MicReported={MicReported}, BytesSent={BytesSent}, Capture={CaptureRate}Hz {CaptureProcessing} on {CaptureDevice}, Sent={SentIsLocal}/{SentLabel}, SendCodec={SendCodec}, RttSource={RttSource}, RemoteLoss={RemoteLoss:F1}%, RemoteJitter={RemoteJitter:F0}ms, Echo={EchoReported}:{Erl:F1}/{Erle:F1}dB, Leg={ProviderLegId}, Ccid={ProviderCallControlId}, Codec={Codec}.",
                    rating,
                    RedactedUserId(),
                    report.CallId.SanitizeLogValue(),
                    report.Mos,
                    report.LossPercent,
                    report.JitterMs,
                    report.RoundTripTimeMs,
                    report.JitterBufferMs,
                    report.ConcealmentPercent,
                    report.InboundLevel,
                    report.CaptureProbeLevel,
                    report.BytesReceived,
                    report.MicrophoneLevel,
                    report.CaptureReported,
                    report.BytesSent,
                    report.CaptureSampleRate,
                    report.CaptureProcessing.SanitizeLogValue(),
                    report.CaptureDevice.SanitizeLogValue(),
                    report.SentTrackReported ? (report.SentTrackIsLocalStream ? "local" : "OTHER") : "n/a",
                    report.SentTrackLabel.SanitizeLogValue(),
                    report.SendCodec.SanitizeLogValue(),
                    report.RttSource.SanitizeLogValue(),
                    report.RemoteFractionLostPercent,
                    report.RemoteJitterMs,
                    report.EchoStatsReported,
                    report.EchoReturnLossDb,
                    report.EchoReturnLossEnhancementDb,
                    report.ProviderLegId.SanitizeLogValue(),
                    report.ProviderCallControlId.SanitizeLogValue(),
                    report.Codec.SanitizeLogValue());
            }
        });
    }
}
