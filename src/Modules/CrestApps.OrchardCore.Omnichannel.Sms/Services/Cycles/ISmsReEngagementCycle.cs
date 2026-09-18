using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Proactively re-engages automated SMS contacts who have gone quiet, when the loading campaign enabled it.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ISmsReEngagementCycle : IBackgroundCycle;
