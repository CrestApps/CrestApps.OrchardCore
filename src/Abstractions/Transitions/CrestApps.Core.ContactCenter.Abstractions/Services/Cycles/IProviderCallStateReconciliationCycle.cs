using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Revalidates active provider-backed interactions against the telephony server so restarts and missed live events do not leave queued voice work out of sync.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IProviderCallStateReconciliationCycle : IBackgroundCycle;
