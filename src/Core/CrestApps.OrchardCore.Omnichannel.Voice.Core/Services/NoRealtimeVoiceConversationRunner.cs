namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The realtime runner for a tenant that cannot carry live call audio. It reports that no session ran, so the
/// call falls back to the turn-based conversation instead of sitting silent.
/// </summary>
/// <remarks>
/// Speech-to-speech needs a bidirectional media path to the caller's leg, which the Contact Center Voice Media
/// feature provides. Automated Voice does not depend on that feature — a tenant can run automated calls with only
/// speak and transcribe — so the capability has to be optional rather than assumed.
/// </remarks>
public sealed class NoRealtimeVoiceConversationRunner : IRealtimeVoiceConversationRunner
{
    /// <inheritdoc/>
    public Task<bool> RunAsync(RealtimeVoiceConversationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
