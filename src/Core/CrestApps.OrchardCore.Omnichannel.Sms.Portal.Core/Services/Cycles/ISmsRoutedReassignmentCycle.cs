using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Periodically returns routed (push-assigned) SMS conversations that the assigned agent has not picked up within the grace window to their queue's shared pool, so a message never stalls in one inbox.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ISmsRoutedReassignmentCycle : IBackgroundCycle;
