using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Telnyx.Core.Services;

/// <summary>
/// Periodically evaluates soft-phone health -- browser credential issuance (a proxy for registration success) and inbound webhook processing -- and logs a snapshot, warning when the credential issuance success rate falls below the alert threshold so a broken registration path surfaces without waiting for an agent to report it.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ISoftPhoneHealthCanaryCycle : IBackgroundCycle;
