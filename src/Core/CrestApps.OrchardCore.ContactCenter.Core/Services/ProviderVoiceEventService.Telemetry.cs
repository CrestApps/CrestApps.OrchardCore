using System.Diagnostics;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Telemetry;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Telemetry;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What the call stream reports about itself: what became of each provider event, how late it arrived, and what each
/// call came to when it ended. Every provider (Telnyx, Asterisk, any other) feeds the stream here, so this is the one
/// call funnel they share.
/// </summary>
/// <remarks>
/// Each <see cref="IngestAsync(ProviderVoiceEvent, CancellationToken)"/> call records exactly one result. One that
/// loses a concurrency race records <c>retried</c>, and the retry, which runs the method again in a fresh scope,
/// records its own; a call's end is recorded once its transition has committed, so a lost race never counts a call
/// twice.
/// </remarks>
public sealed partial class ProviderVoiceEventService
{
    private async Task<CallSession> RetryInFreshScopeAsync(
        ProviderVoiceEvent providerEvent,
        Activity activity,
        CancellationToken cancellationToken)
    {
        ContactCenterDiagnostics.RecordCasConflict("provider_event.ingest");
        ContactCenterDiagnostics.RecordProviderEvent(
            activity,
            _providerIdentityResolver.Canonicalize(providerEvent.ProviderName),
            providerEvent.State,
            ContactCenterDiagnostics.ProviderEventRetried,
            lag: null);

        Exception lastException = null;

        for (var attempt = 2; attempt <= MaxIngestionAttempts; attempt++)
        {
            CallSession session = null;

            ContactCenterLog.ProviderVoiceEventRetried(_logger, providerEvent.ProviderCallId.SanitizeLogValue(), attempt, MaxIngestionAttempts);

            try
            {
                await _scopeExecutor.ExecuteAsync<IProviderVoiceEventService>(async service =>
                {
                    session = await service.IngestAsync(providerEvent, cancellationToken);
                });

                return session;
            }
            catch (ConcurrencyException exception)
            {
                lastException = exception;
            }
        }

        throw lastException ?? new ConcurrencyException(new Document());
    }

    /// <summary>
    /// Records a provider event ordering discarded as stale. The discard is counted once, by why, on the telephony
    /// instrument every provider stream shares (<c>crestapps.telephony.voice_event.discards</c>); the call stream only
    /// marks its span and records the event's lag.
    /// </summary>
    private void RecordDiscard(Activity activity, ProviderVoiceEvent providerEvent, string reason)
    {
        activity?.SetTag(CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        TelephonyTelemetry.RecordVoiceEventDiscard(providerEvent.ProviderName, reason);
        ContactCenterDiagnostics.RecordProviderEvent(
            activity,
            providerEvent.ProviderName,
            providerEvent.State,
            ContactCenterDiagnostics.ProviderEventStale,
            GetLag(providerEvent),
            count: false);
    }

    /// <summary>
    /// Records what became of one provider event that did not change the call.
    /// </summary>
    private void RecordIngest(Activity activity, ProviderVoiceEvent providerEvent, string result)
        => ContactCenterDiagnostics.RecordProviderEvent(activity, providerEvent.ProviderName, providerEvent.State, result, GetLag(providerEvent));

    /// <summary>
    /// Records a provider event that changed the call, once its change committed, and what the call came to when the
    /// event is the one that ended it.
    /// </summary>
    private void RecordIngestApplied(
        Activity activity,
        ProviderVoiceEvent providerEvent,
        CallSession session,
        Interaction interaction,
        VoiceCallState previousState,
        bool wasClassified)
    {
        activity?.SetTag("crestapps.contactcenter.interaction.id", interaction.ItemId);
        activity?.SetTag("crestapps.contactcenter.call_session.id", session.ItemId);

        ContactCenterDiagnostics.RecordProviderEvent(
            activity,
            providerEvent.ProviderName,
            providerEvent.State,
            ContactCenterDiagnostics.ProviderEventApplied,
            GetLag(providerEvent));

        if (!wasClassified && providerEvent.AnswerClassification is { } classification)
        {
            ContactCenterDiagnostics.RecordAnswerClassification(classification);
        }

        if (!IsTerminalState(previousState) && IsTerminalState(session.State))
        {
            ContactCenterDiagnostics.RecordCallEnded(
                session,
                interaction,
                ReadMetadata(providerEvent, ContactCenterConstants.TelephonyMetadata.HangupSource));
        }
    }

    /// <summary>
    /// Logs an event that matched no interaction, with the caller's and the dialed addresses redacted.
    /// </summary>
    private void LogUnmatchedEvent(ProviderVoiceEvent providerEvent)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var providerCallId = providerEvent.ProviderCallId.SanitizeLogValue();
        var fromAddress = RedactAddress(providerEvent.FromAddress);
        var toAddress = RedactAddress(providerEvent.ToAddress);

        ContactCenterLog.UnmatchedProviderVoiceEvent(_logger, providerCallId, providerEvent.State, providerEvent.HangupCause, fromAddress, toAddress);
    }

    private string RedactAddress(string address)
        => string.IsNullOrEmpty(address) ? address : _addressRedactor.Redact(address);

    private TimeSpan? GetLag(ProviderVoiceEvent providerEvent)
        => providerEvent.OccurredUtc is { } occurredUtc ? _clock.UtcNow - occurredUtc : null;
}
