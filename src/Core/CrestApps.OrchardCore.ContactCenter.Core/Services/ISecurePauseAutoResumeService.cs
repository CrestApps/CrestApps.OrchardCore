namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Force-resumes recordings that have stayed paused past the tenant's maximum secure-pause window. The guard
/// exists so a sensitive-data pause that is never explicitly resumed — because a call dropped, a browser closed,
/// or an agent walked away — cannot silently suppress capture for the remainder of a compliance-recorded call.
/// </summary>
public interface ISecurePauseAutoResumeService
{
    /// <summary>
    /// Resumes every recording whose pause has outlived the tenant's configured maximum secure-pause window,
    /// bounded per invocation, and returns the number of recordings resumed.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of recordings that were force-resumed.</returns>
    Task<int> ResumeExpiredAsync(CancellationToken cancellationToken = default);
}
