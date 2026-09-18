using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Expires stale agent reservations and assigns waiting work to available agents across enabled queues, and across the virtual campaign queues that carry agent-driven (Preview/Manual) outbound inventory — which the enabled-queue sweep cannot see because campaign queues are never persisted.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IReservationExpiryCycle : IBackgroundCycle;
