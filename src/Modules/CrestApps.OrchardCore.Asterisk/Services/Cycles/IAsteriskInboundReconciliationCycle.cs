using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Periodically reconciles this tenant's durable Asterisk channel bindings against live ARI state so a stranded resource is recovered even when the real-time listener's WebSocket never dropped.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
internal interface IAsteriskInboundReconciliationCycle : IBackgroundCycle;
