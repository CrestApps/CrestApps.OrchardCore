namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Saves the pending response changes now, so a concurrent change is caught and reported instead of failing the
/// request later.
/// </summary>
public interface ISurveyResponseCommitter
{
    /// <summary>
    /// Saves the pending changes.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when saved; <see langword="false"/> when someone changed the response meanwhile.</returns>
    Task<bool> CommitAsync(CancellationToken cancellationToken = default);
}
