using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Revalidates policy that may have changed after a pending provider command was persisted but before crash
/// recovery attempts to dispatch it.
/// </summary>
public interface IProviderCommandDispatchValidator
{
    /// <summary>
    /// Determines whether a pending command may still be dispatched.
    /// </summary>
    /// <param name="command">The pending command being recovered.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when dispatch remains allowed; otherwise, <see langword="false"/>.</returns>
    Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default);
}
