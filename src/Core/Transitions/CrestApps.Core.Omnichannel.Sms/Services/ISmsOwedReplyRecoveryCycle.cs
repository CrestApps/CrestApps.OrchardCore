using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.Omnichannel.Sms.Services;

/// <summary>
/// Recovers automated SMS conversations whose reply was owed but never sent.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ISmsOwedReplyRecoveryCycle : IBackgroundCycle;
