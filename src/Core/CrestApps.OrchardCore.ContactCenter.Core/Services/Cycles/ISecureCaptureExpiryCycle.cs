using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Expires secure capture sessions whose customer window has elapsed without a submission, so a capture a customer never completed is settled and any recording pause it engaged is resumed rather than left suppressing capture indefinitely.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ISecureCaptureExpiryCycle : IBackgroundCycle;
