namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Defines the contract for bulk manage activity filter handlers.
/// Implement this interface to add custom filtering logic that extends the bulk manage activities query.
/// </summary>
public interface IBulkManageActivityFilterHandler
{
    /// <summary>
    /// Applies additional filtering logic to the bulk manage activity query context.
    /// </summary>
    /// <param name="context">The filter context containing the filter criteria and query to update.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task FilteringAsync(BulkManageActivityFilterContext context, CancellationToken cancellationToken = default);
}
