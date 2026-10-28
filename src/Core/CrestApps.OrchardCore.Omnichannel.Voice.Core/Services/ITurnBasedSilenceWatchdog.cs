using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Notices when nobody has spoken on a turn-based automated call, so the call is prompted and then ended rather
/// than held open on a silent line.
/// </summary>
/// <remarks>
/// A turn-based call moves only when the provider tells it something happened: the assistant finished speaking,
/// or the caller said something. Silence produces no event at all, so a call nobody answers -- a voicemail that
/// has finished its greeting, a person who put the phone down -- waited for one indefinitely. Live, a voicemail
/// recorded more than a minute and a half of nothing until the carrier cut it off.
/// </remarks>
public interface ITurnBasedSilenceWatchdog
{
    /// <summary>
    /// Starts watching the listening turn that has just begun.
    /// </summary>
    /// <remarks>
    /// Returns once the watch is scheduled, not when it fires: the request that began listening is a provider
    /// webhook and must be answered promptly, so the wait runs on a scope of its own.
    /// </remarks>
    /// <param name="silence">The listening turn to watch.</param>
    Task ArmAsync(TurnBasedSilence silence);

    /// <summary>
    /// Records that the caller has just been heard, so no watch armed before now takes the line for silent.
    /// </summary>
    /// <remarks>
    /// Called as soon as the caller's words arrive, not once they are stored. The turn that stores them commits
    /// when its webhook request ends, after the reply has been generated; a watch that fired in between found no
    /// line from the caller. Live, a caller who said hello was taken for a voicemail 30 ms before their line was
    /// saved, was left a message and was hung up on.
    /// </remarks>
    /// <param name="activityId">The call's activity.</param>
    void CallerHeard(string activityId);

    /// <summary>
    /// Forgets what was heard on a call that has ended.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    void Forget(string activityId);
}
