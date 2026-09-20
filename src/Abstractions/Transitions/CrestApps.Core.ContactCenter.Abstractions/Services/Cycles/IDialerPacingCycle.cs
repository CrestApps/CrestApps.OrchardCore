using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Runs one pacing cycle for each enabled dialer profile so power and progressive campaigns dial automatically.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface IDialerPacingCycle : IBackgroundCycle;
