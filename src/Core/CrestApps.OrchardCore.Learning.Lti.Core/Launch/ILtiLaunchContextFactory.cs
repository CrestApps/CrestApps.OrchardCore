using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Gathers what a launch message says from its message hint.
/// </summary>
public interface ILtiLaunchContextFactory
{
    /// <summary>
    /// Gathers the launch.
    /// </summary>
    /// <param name="hint">The message hint, already checked.</param>
    /// <param name="tool">The tool.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The launch, or <see langword="null"/> when the person, the course or the course item is gone, or the
    /// item no longer launches this tool.</returns>
    Task<LtiLaunchContext> CreateAsync(LtiMessageHint hint, ExternalTool tool, CancellationToken cancellationToken = default);
}
