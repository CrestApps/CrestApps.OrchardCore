using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Runs a caller through an entry point's phone menu.
/// </summary>
public interface IIvrExecutionService
{
    /// <summary>
    /// Plays the first menu, or reports that this entry point has none.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="flow">The menu on the entry point, when it has one.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IvrStep> StartAsync(Interaction interaction, IvrFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a key press and returns what the caller should hear or be routed to next.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="flow">The menu on the entry point.</param>
    /// <param name="digits">What the caller pressed, or <see langword="null"/> when they pressed nothing.</param>
    /// <param name="deliveryId">The provider's identifier for this delivery, so a redelivery is recognised.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IvrStep> HandleDigitsAsync(
        Interaction interaction,
        IvrFlow flow,
        string digits,
        string deliveryId,
        CancellationToken cancellationToken = default);
}
