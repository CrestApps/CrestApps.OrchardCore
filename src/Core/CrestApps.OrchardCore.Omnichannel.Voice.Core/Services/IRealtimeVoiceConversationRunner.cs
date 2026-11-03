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
    /// Gets or sets a function reporting how many times the model has asked to end the call.
    /// </summary>
    /// <remarks>
    /// <see cref="EndCallRequested"/> fires once, for the first request. A customer who answers the goodbye hands
    /// the call back, and the model then ends it again; this is how the session hears that second request. Left
    /// unset, only the first request closes the call.
    /// </remarks>
    public Func<int> EndCallRequests { get; set; }

    /// <summary>
    /// Gets or sets what is told each line the assistant finishes, so a question it asked keeps the call open until
    /// the customer answers. Left unset, nothing is.
    /// </summary>
    public Action<string> AssistantSaid { get; set; }

    /// <summary>
    /// Gets or sets what is told when the customer says something.
    /// </summary>
    public Action CustomerAnswered { get; set; }

    /// <summary>
    /// Gets or sets a token that is cancelled when the telephony provider reports that a voicemail or answering
    /// machine answered the call.
    /// </summary>
    /// <remarks>
    /// The report arrives on a webhook request of its own while the session holds the call in another, so it is
    /// carried here through <see cref="IAnsweringMachineSignals"/>. Once it fires the session stops conversing: what
    /// the model says to the greeting is kept off the line, one message is left once the greeting is over, and the
    /// call is ended through <see cref="EndCallOnVoicemail"/>. Left unset, a machine is treated like anybody else who
    /// answers, and only the model can notice it.
    /// </remarks>
    public CancellationToken AnsweredByMachine { get; set; }

    /// <summary>
    /// Gets or sets a token that is cancelled when the provider hears the machine's greeting end, which is the moment
    /// to leave the message. Left unset, or never fired, the message is left once the line has been quiet for a moment.
    /// </summary>
    public CancellationToken MachineGreetingEnded { get; set; }

    /// <summary>
    /// Gets or sets a token that is cancelled when the assistant presses keys on the call. The machine that answered
    /// is then a phone menu, not a voicemail: no message is left on it, and the conversation carries on with whoever
    /// the menu puts the call through to.
    /// </summary>
    public CancellationToken PhoneMenuReached { get; set; }

    /// <summary>
    /// Gets or sets what is told when the session has left its message on the machine, or has given up on one, so
    /// the call is ended exactly as the end-call tool ends it with voicemail reported: <see cref="EndCallRequested"/>
    /// fires and <see cref="ReachedVoicemail"/> reads <see langword="true"/>.
    /// </summary>
    public Action EndCallOnVoicemail { get; set; }

    /// <summary>
    /// Gets or sets what is told when the session has said goodbye on a line that stayed quiet through its prompts,
    /// so the call is ended exactly as the end-call tool ends it: <see cref="EndCallRequested"/> fires, the goodbye
    /// plays, and the line closes. Left unset, the session stops asking and the line stays open.
    /// </summary>
    public Action EndCallOnSilence { get; set; }

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
    /// Gets or sets the guidance for a call that would hand the caller to a person but cannot now, the queue being
    /// closed, or <see langword="null"/> otherwise. It is given without the transfer tool.
    /// </summary>
    public string NoHandoffInstructions { get; set; }

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

    /// <summary>
    /// Gets or sets the notice that the call is recorded, which the assistant must give word for word before
    /// anything else, or <see langword="null"/> when the tenant gives none on automated voice calls.
    /// </summary>
    public string RecordingDisclosure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the live session was lost while the caller was still on the line,
    /// and could not be brought back.
    /// </summary>
    /// <remarks>
    /// Set by the runner, read by whoever held the call. A session that ends this way has decided nothing — no
    /// transfer, no goodbye — so without it the caller is left on an open line with nobody talking to them. Live,
    /// one provider error ended the session and the caller sat in fifty seconds of silence before hanging up.
    /// </remarks>
    public bool SessionLost { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the assistant spoke to the caller after their last words and before it
    /// asked to hand them to a person, so the handoff need not tell them it is connecting them.
    /// </summary>
    public bool HandoffAnnounced { get; set; }

    /// <summary>
    /// Gets or sets the token that fires when this node begins to shut down.
    /// </summary>
    /// <remarks>
    /// The conversation runs in this process, so it cannot outlive it. When the token fires the session is ended
    /// and reported lost, which hands the caller on the way a lost session always does, rather than leaving them
    /// on a line that falls silent when the process exits.
    /// </remarks>
    public CancellationToken NodeStopping { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the session was ended because this node is shutting down.
    /// </summary>
    public bool EndedByShutdown { get; set; }

    /// <summary>
    /// Gets or sets what the session measures from its audio for the usage report: started once the live session
    /// is holding the call, and stopped when it ends.
    /// </summary>
    internal AIVoiceSessionMeter Meter { get; set; }
}
