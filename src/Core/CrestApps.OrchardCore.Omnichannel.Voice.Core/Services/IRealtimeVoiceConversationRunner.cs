using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Runs an automated phone conversation as a live speech-to-speech session rather than as a loop of
/// transcribe, complete, and synthesize.
/// </summary>
/// <remarks>
/// The difference is what the caller experiences. The turn-based loop cannot start a reply until the caller has
/// stopped, the transcript has come back, the model has answered and the answer has been synthesized — seconds of
/// dead air per turn, and no way to interrupt. A realtime session streams both directions at once, so the
/// assistant begins answering while the caller is still finishing, and a caller who talks over it is heard.
/// </remarks>
public interface IRealtimeVoiceConversationRunner
{
    /// <summary>
    /// Holds the whole conversation, returning when the call ends.
    /// </summary>
    /// <param name="context">Who is being called, on what, and with which profile.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a realtime session actually ran.</returns>
    Task<bool> RunAsync(RealtimeVoiceConversationContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a realtime voice conversation needs to know about the call it is running on.
/// </summary>
public sealed class RealtimeVoiceConversationContext
{
    /// <summary>
    /// Gets or sets the activity the call belongs to.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the AI profile driving the conversation.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the chat session the transcript is recorded on, so the call is concluded and summarized the
    /// same way a turn-based one is.
    /// </summary>
    public AIChatSession Session { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the telephony provider carrying the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the caller's leg.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the call is tracked by, when there is one.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call carries a quiet background bed — room tone, and the sound
    /// of the agent typing while they talk — instead of arriving on a dead-silent line.
    /// </summary>
    /// <remarks>
    /// Off by default. Perfect silence between sentences is a strong tell that nobody is there, but the bed is a
    /// deliberate choice: it is audible on the call, and an operator should opt into it rather than discover it.
    /// </remarks>
    public bool UseCallAmbience { get; set; }

    /// <summary>
    /// Gets or sets a token that is cancelled when the model asks to hand the caller to a live agent. The session
    /// finishes the sentence it is on and then closes, so the caller is put through to the queue instead of
    /// staying with an assistant that has already told them a person is coming.
    /// </summary>
    public CancellationToken HandoffRequested { get; set; }
}
