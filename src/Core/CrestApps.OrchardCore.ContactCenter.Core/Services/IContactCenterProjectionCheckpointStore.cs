using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for projection replay checkpoints.
/// </summary>
public interface IContactCenterProjectionCheckpointStore : ICatalog<ContactCenterProjectionCheckpoint>
{
    /// <summary>
    /// Finds the checkpoint recorded for the specified projection handler.
    /// </summary>
    /// <param name="handlerId">The stable, versioned projection handler identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching checkpoint, or <see langword="null"/> when none exists.</returns>
    Task<ContactCenterProjectionCheckpoint> FindByHandlerAsync(string handlerId, CancellationToken cancellationToken = default);
}
