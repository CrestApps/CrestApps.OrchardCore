using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.Telephony.Asterisk.Services;

/// <summary>
/// Periodically reclaims expired browser SIP credentials so orphaned PJSIP realtime rows do not accumulate in the Asterisk realtime store once their issued lifetime has elapsed.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IAsteriskPjsipCredentialCleanupCycle : IBackgroundCycle;
