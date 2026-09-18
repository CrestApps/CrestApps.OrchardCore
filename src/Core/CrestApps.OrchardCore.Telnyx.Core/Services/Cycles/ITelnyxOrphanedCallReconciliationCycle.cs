using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Telnyx.Core.Services;

/// <summary>
/// Asks the provider what calls it actually has up, and acts on the ones this platform has no record of.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ITelnyxOrphanedCallReconciliationCycle : IBackgroundCycle;
