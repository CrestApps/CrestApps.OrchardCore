using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Represents the automated activities processor background task.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IAutomatedActivitiesProcessorCycle : IBackgroundCycle;
