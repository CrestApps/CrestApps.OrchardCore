using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Recovers Contact Center agents whose after-call work was orphaned or exceeded its deadline.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IAgentAvailabilityRecoveryCycle : IBackgroundCycle;
