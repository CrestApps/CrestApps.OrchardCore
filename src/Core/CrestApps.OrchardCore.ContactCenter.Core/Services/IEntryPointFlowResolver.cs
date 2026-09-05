using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Finds the phone menu that belongs to a call in progress.
/// </summary>
/// <remarks>
/// A key press arrives with nothing but the provider's call id, and the menu lives on the entry point the caller
/// dialled. This is the lookup between the two.
/// </remarks>
public interface IEntryPointFlowResolver
{
    /// <summary>
    /// Returns the menu on the entry point this call came in on, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IvrFlow> FindFlowAsync(Interaction interaction, CancellationToken cancellationToken = default);
}
