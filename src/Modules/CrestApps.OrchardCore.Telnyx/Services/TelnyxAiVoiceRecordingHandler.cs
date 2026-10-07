using YesSql;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Records an automated voice agent's call from the moment it is answered, when the tenant records every call, so it
/// can be played back beside its transcript on the call recordings page.
/// </summary>
/// <remarks>
/// What the recording writes is committed at once. The answered webhook this runs in is held open by a realtime
/// conversation for as long as the AI talks, and a write left pending in that request's session kept the database's
/// write lock until the conversation stored its first line -- after the answering-machine check, some fifteen seconds
/// on a voicemail. On SQLite every other write in the tenant waited behind it: agents' pages failed to connect while an
/// automated call was waiting to greet. The conversation commits each turn for the same reason.
/// </remarks>
public sealed class TelnyxAiVoiceRecordingHandler : ITelnyxAiVoiceEventHandler
{
    private readonly ITelnyxAutomaticCallRecorder _recorder;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAiVoiceRecordingHandler"/> class.
    /// </summary>
    /// <param name="recorder">The recorder that starts the recording when the tenant's policy allows it.</param>
    /// <param name="session">The request's session, committed once the recording is listed.</param>
    public TelnyxAiVoiceRecordingHandler(ITelnyxAutomaticCallRecorder recorder, ISession session)
    {
        _recorder = recorder;
        _session = session;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken = default)
    {
        if (callEvent is null ||
            state is null ||
            !string.Equals(callEvent.EventType?.Trim(), "call.answered", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Telnyx calls a leg the caller placed "incoming"; the customer is whoever is not the platform's number.
        var isInbound = string.Equals(callEvent.Direction?.Trim(), "incoming", StringComparison.OrdinalIgnoreCase);

        if (await _recorder.RecordAiCallAsync(
            callEvent.CallControlId,
            state.ActivityId,
            isInbound ? callEvent.From : callEvent.To,
            isInbound,
            cancellationToken))
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
    }
}
