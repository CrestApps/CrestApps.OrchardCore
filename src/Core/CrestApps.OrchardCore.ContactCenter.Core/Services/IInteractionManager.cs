using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for interactions.
/// </summary>
public interface IInteractionManager
{
    /// <summary>
    /// Creates a new interaction instance with default values.
    /// </summary>
    /// <returns>The new interaction.</returns>
    ValueTask<Interaction> NewAsync();

    /// <summary>
    /// Creates the specified interaction.
    /// </summary>
    /// <param name="interaction">The interaction to create.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask CreateAsync(Interaction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the specified interaction.
    /// </summary>
    /// <param name="interaction">The interaction to update.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask UpdateAsync(Interaction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the interaction with the specified identifier.
    /// </summary>
    /// <param name="id">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching interaction, or <see langword="null"/> when none is found.</returns>
    ValueTask<Interaction> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the interaction linked to the specified CRM activity.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching interaction, or <see langword="null"/> when none is found.</returns>
    Task<Interaction> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the interaction with the specified correlation identifier.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching interaction, or <see langword="null"/> when none is found.</returns>
    Task<Interaction> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages interactions that are currently in the specified status.
    /// </summary>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="status">The status to filter by.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The page of matching interactions.</returns>
    Task<PageResult<Interaction>> PageByStatusAsync(int page, int pageSize, InteractionStatus status, CancellationToken cancellationToken = default);
}
