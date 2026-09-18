using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Periodically reconciles in-progress telephony interactions with provider-authoritative state.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ITelephonyInteractionReconciliationCycle : IBackgroundCycle;
