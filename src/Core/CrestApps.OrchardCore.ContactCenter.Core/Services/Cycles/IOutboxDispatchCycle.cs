using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Redelivers Contact Center domain events whose handler dispatch previously failed.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IOutboxDispatchCycle : IBackgroundCycle;
