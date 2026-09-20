using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Recovers due provider commands so ambiguous or interrupted provider operations are resumed through the durable provider-command state machine.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IProviderCommandRecoveryCycle : IBackgroundCycle;
