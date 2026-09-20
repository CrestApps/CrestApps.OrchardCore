using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Enforces agent ring windows at a fine granularity.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IDirectRingTimeoutCycle : IBackgroundCycle;
