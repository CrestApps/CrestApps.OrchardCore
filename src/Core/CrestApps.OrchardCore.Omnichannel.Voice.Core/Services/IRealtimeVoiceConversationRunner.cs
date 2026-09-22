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

    /// <summary>
    /// Gets or sets a token that is cancelled when the model reports the conversation finished. The session
    /// lets the closing line play, leaves the caller a moment to add anything, and then ends the call — so the
    /// customer is not left holding a line that nobody is going to hang up.
    /// </summary>
    public CancellationToken EndCallRequested { get; set; }

    /// <summary>
    /// Gets or sets a function reporting whether the model, when it ended the call, said it had reached voicemail.
    /// </summary>
    /// <remarks>
    /// Read once <see cref="EndCallRequested"/> has fired. A recording has nobody to give a moment to, so the call
    /// is hung up as soon as the message has played rather than leaving it recording silence.
    /// </remarks>
    public Func<bool> ReachedVoicemail { get; set; }

    /// <summary>
    /// Gets or sets the guidance telling the model when to hand the caller to a live agent, or
    /// <see langword="null"/> when this call has nowhere to hand them.
    /// </summary>
    /// <remarks>
    /// A realtime session is configured once from the profile, so unlike a turn-based completion it never sees
    /// the per-call handoff guidance the flow settings produce. Without this the model on a live call is not told
    /// that escalating is possible — and is given no tool to do it with — so a caller asking for a person was
    /// talked to by the assistant instead.
    /// </remarks>
    public string HandoffInstructions { get; set; }

    /// <summary>
    /// Gets or sets the deployment this call is held on.
    /// </summary>
    /// <remarks>
    /// Realtime is a capability a model either has or does not, rather than a deployment of its own, so the call
    /// is held on the profile's chat deployment when that deployment declares it. The loop resolves it, because
    /// the same answer decides whether there is a live session at all.
    /// </remarks>
    public string RealtimeDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the name of the person being called, when the contact has one.
    /// </summary>
    /// <remarks>
    /// The turn-based greeting is rendered from a template that has the contact in scope, so it can open with
    /// their name. A live session is configured from the profile and never sees that template — and a model asked
    /// to open a sales call with no name does not decline to use one, it invents a plausible one. Observed live:
    /// the assistant opened "is this Marcus?" to a contact named Amani, who reasonably asked who it was looking
    /// for, which the assistant then read as a request for a human and transferred the call.
    /// </remarks>
    public string ContactName { get; set; }
}
