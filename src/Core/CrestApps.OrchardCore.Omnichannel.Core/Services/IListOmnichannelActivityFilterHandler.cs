namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Defines the contract for list omnichannel activity filter handler.
/// </summary>
public interface IListOmnichannelActivityFilterHandler
{
    /// <summary>
    /// Applies additional filtering logic to the omnichannel activity query context.
    /// </summary>
    /// <param name="context">The filter context to update.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task FilteringAsync(ListOmnichannelActivityFilterContext context, CancellationToken cancellationToken = default);
}
