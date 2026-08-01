using CrestApps.Core.AI.Tooling;

namespace CrestApps.OrchardCore.AI.Tools.Services;

/// <summary>
/// Provides the AI tool instances the current user is allowed to assign to a model.
/// </summary>
public interface IAIToolInstanceAccessor
{
    /// <summary>
    /// Gets the stored tool instances the current user is authorized to use.
    /// Returns an empty collection when the tool instances feature is disabled or when there is no current user.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The accessible tool instances.</returns>
    Task<IList<AIToolInstance>> GetAccessibleInstancesAsync(CancellationToken cancellationToken = default);
}
