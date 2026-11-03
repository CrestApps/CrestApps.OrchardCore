namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Carries what the telephony provider reports about who answered an automated call -- a voicemail or answering
/// machine, and then the end of its greeting -- from the webhook request that receives it to the live session that
/// is holding the call in another request.
/// </summary>
/// <remarks>
/// <para>
/// A realtime session holds the call inside the provider's "call answered" webhook request for the whole call. The
/// provider's detection arrives seconds later on a webhook request of its own, and nothing in that request can reach
/// the session: the activity it marks is read by the session only once the call is over. So the detection is also
/// published here, keyed by the call, and the session watches for it while it holds the line.
/// </para>
/// <para>
/// Each call's signals are kept until the call is forgotten, whichever of the two requests gets there first: a
/// detection reported before the session starts watching is still seen when it does.
/// </para>
/// </remarks>
public interface IAnsweringMachineSignals
{
    /// <summary>
    /// Records that the provider heard a voicemail or answering machine answer the call.
    /// </summary>
    /// <param name="providerName">The technical name of the telephony provider carrying the call.</param>
    /// <param name="providerCallId">The provider's identifier for the call leg.</param>
    void MachineDetected(string providerName, string providerCallId);

    /// <summary>
    /// Records that the provider heard the machine's greeting end, which is the moment to leave the message.
    /// </summary>
    /// <param name="providerName">The technical name of the telephony provider carrying the call.</param>
    /// <param name="providerCallId">The provider's identifier for the call leg.</param>
    void GreetingEnded(string providerName, string providerCallId);

    /// <summary>
    /// Records that the assistant pressed keys on the call, so the machine that answered is a phone menu it is getting
    /// through rather than a voicemail to leave a message on.
    /// </summary>
    /// <remarks>
    /// A business's phone menu is a machine as far as the provider's detection can tell. Live, the assistant pressed
    /// the key for the person it was calling, and the call was taken for a voicemail, left a message and hung up on.
    /// </remarks>
    /// <param name="providerName">The technical name of the telephony provider carrying the call.</param>
    /// <param name="providerCallId">The provider's identifier for the call leg.</param>
    void PhoneMenuReached(string providerName, string providerCallId);

    /// <summary>
    /// The signals for one call, to watch while the call is held.
    /// </summary>
    /// <param name="providerName">The technical name of the telephony provider carrying the call.</param>
    /// <param name="providerCallId">The provider's identifier for the call leg.</param>
    /// <returns>The call's signals, already set when the provider reported before anybody watched.</returns>
    AnsweringMachineSignal Watch(string providerName, string providerCallId);

    /// <summary>
    /// Forgets a call that is over, so nothing is kept for it.
    /// </summary>
    /// <param name="providerName">The technical name of the telephony provider carrying the call.</param>
    /// <param name="providerCallId">The provider's identifier for the call leg.</param>
    void Forget(string providerName, string providerCallId);
}
