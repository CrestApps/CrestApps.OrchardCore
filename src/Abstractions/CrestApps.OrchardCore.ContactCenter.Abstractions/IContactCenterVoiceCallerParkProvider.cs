namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// A server-side ACD voice provider whose agent leg is joined to the caller in a way that ending the agent leg also
/// ends the caller, unless the caller is first taken out of that join.
/// </summary>
/// <remarks>
/// A blind transfer to a queue or to another agent takes the transferring agent off a call that carries on without
/// them: the caller is held, and the agent's leg is hung up. On a provider that bridges the two legs and hangs up the
/// remaining leg when its peer goes, that hangup drops the caller the transfer was meant to keep. Such a provider
/// implements this so the caller is parked -- still on the line, and free to be played hold music and joined to the
/// next agent -- before the agent's leg is released.
/// </remarks>
public interface IContactCenterVoiceCallerParkProvider
{
    /// <summary>
    /// Takes the caller out of the agent's bridge and leaves them parked on the line.
    /// </summary>
    /// <param name="providerCallId">The provider identifier of the caller's leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the caller no longer depends on the agent's leg, so it can be hung up without them;
    /// <see langword="false"/> when the caller is still joined to it.
    /// </returns>
    Task<bool> ParkCallerAsync(string providerCallId, CancellationToken cancellationToken = default);
}
