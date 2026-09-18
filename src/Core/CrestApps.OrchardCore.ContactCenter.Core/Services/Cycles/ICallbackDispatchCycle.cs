using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Promotes due callbacks into outbound activities so the dialer or an agent can handle them.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ICallbackDispatchCycle : IBackgroundCycle;
