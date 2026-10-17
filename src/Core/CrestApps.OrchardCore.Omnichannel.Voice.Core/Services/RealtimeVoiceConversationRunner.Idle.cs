namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a live call does when the line goes quiet on both sides.
/// </summary>
/// <remarks>
/// Split from the runner because the runner's file is near the size the architecture guard allows.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// The step the idle watchdog takes on a quiet line.
    /// </summary>
    internal enum IdleStep
    {
        /// <summary>
        /// Not quiet for long enough yet.
        /// </summary>
        Wait,

        /// <summary>
        /// Ask whether the caller is still there.
        /// </summary>
        AskIfStillThere,

        /// <summary>
        /// Say goodbye and end the call: the caller was asked as many times as is worth asking.
        /// </summary>
        SayGoodbye,
    }

    /// <summary>
    /// Decides what a quiet line calls for.
    /// </summary>
    /// <remarks>
    /// Each step comes after the same silence, so a silent call ends about three silences after the assistant's last
    /// word. Live, a session that had asked twice simply stopped asking and held the line open, unheard, for five
    /// minutes, until the caller hung up.
    /// </remarks>
    /// <param name="promptsAsked">How many times the caller has been asked whether they are still there.</param>
    /// <param name="quietFor">How long the line has been quiet on both sides.</param>
    /// <param name="responseInFlight">Whether the model is producing a response that has not finished yet.</param>
    internal static IdleStep NextIdleStep(int promptsAsked, TimeSpan quietFor, bool responseInFlight = false)
    {
        if (quietFor < IdleBeforeSpeakingUp)
        {
            return IdleStep.Wait;
        }

        // A standalone message may only be asked for when no response is in flight: on a session that answers by
        // itself, one on top of another is two replies talking over each other.
        if (responseInFlight && quietFor < IdleBeforeSpeakingUp + IdleResponseInFlightLimit)
        {
            return IdleStep.Wait;
        }

        return promptsAsked < MaximumIdlePrompts ? IdleStep.AskIfStillThere : IdleStep.SayGoodbye;
    }

    /// <summary>
    /// The instruction for asking whether the caller is still there.
    /// </summary>
    /// <remarks>
    /// Asked as a standalone message, from this instruction alone rather than from the conversation so far. Asked to
    /// continue the conversation, the model filled a silent line by talking for the customer: live, its "idle prompt"
    /// was "I'm looking to schedule a haircut appointment", and its next one answered it. A question that carries
    /// nothing the model has to choose is safest worded for it. It still goes with the session's instructions, so the
    /// model knows who it is; and since it sees no conversation, it is told the call is already under way: live, a
    /// standalone prompt without that was answered with the call's whole opening, recording notice and all.
    /// </remarks>
    /// <param name="latestLine">The assistant's latest line on the call, quoted for its language.</param>
    internal static string StillTherePrompt(string latestLine)
        => PartwayThroughTheCall +
            "The line has gone quiet and the person on the call has not answered. In one short sentence, ask whether " +
            "they are still there. Say nothing else: do not speak for them, do not ask anything new, and do not carry " +
            "on the conversation." + InTheCallsLanguage(latestLine);

    /// <summary>
    /// The instruction for the goodbye on a line that stayed quiet.
    /// </summary>
    /// <param name="latestLine">The assistant's latest line on the call, quoted for its language.</param>
    internal static string SilentLineGoodbyePrompt(string latestLine)
        => PartwayThroughTheCall +
            "The line has stayed quiet since you asked whether the person is still there. In one short sentence, say " +
            "that it seems now is not a good time, that you will let them go, and goodbye. Say nothing else." +
            InTheCallsLanguage(latestLine);

    private const string PartwayThroughTheCall =
        "You are partway through this call and have already greeted the person: do not greet them, do not introduce " +
        "yourself, and do not repeat any notice about the call. ";

    private static string InTheCallsLanguage(string latestLine)
        => string.IsNullOrWhiteSpace(latestLine)
            ? string.Empty
            : " Say it in the same language as these words you said earlier on this call, quoted only to show that " +
                $"language, not as something to answer or carry on from: \"{latestLine.Trim()}\"";
}
