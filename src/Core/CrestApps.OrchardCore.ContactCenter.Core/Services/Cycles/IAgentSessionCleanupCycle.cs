using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Signs out agents whose real-time session heartbeat has gone stale so routing stops targeting a client that is no longer connected.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IAgentSessionCleanupCycle : IBackgroundCycle;
