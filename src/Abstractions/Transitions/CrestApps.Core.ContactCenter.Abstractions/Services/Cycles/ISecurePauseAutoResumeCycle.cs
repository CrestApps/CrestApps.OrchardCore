using CrestApps.Core.Hosting.Background;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Force-resumes recordings that have stayed paused past the tenant's maximum secure-pause window, so a sensitive-data pause that was never explicitly resumed cannot silently suppress capture for the remainder of a compliance-recorded call.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
public interface ISecurePauseAutoResumeCycle : IBackgroundCycle;
