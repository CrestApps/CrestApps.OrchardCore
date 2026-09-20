using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Recovers activities stranded in an intermediate routing status (Reserved, Dialing, AwaitingAgentResponse, AwaitingCustomerAnswer, or InProgress) whose reservation, interaction, and agent state were already released.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IOrphanedActivityRecoveryCycle : IBackgroundCycle;
