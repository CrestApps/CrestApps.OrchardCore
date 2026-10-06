namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Records an automated voice agent's call from the moment it is answered, when the tenant records every call, so it
/// can be played back beside its transcript on the call recordings page.
/// </summary>
public sealed class TelnyxAiVoiceRecordingHandler : ITelnyxAiVoiceEventHandler
{
    private readonly ITelnyxAutomaticCallRecorder _recorder;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAiVoiceRecordingHandler"/> class.
    /// </summary>
    /// <param name="recorder">The recorder that starts the recording when the tenant's policy allows it.</param>
    public TelnyxAiVoiceRecordingHandler(ITelnyxAutomaticCallRecorder recorder)
    {
        _recorder = recorder;
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

        await _recorder.RecordAiCallAsync(
            callEvent.CallControlId,
            state.ActivityId,
            isInbound ? callEvent.From : callEvent.To,
            isInbound,
            cancellationToken);
    }
}
