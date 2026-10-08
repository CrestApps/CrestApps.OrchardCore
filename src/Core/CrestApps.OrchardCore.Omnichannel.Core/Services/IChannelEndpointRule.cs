using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// A rule another feature adds to the channel endpoints it extends, such as the settings it stores in an endpoint's
/// properties. The endpoint's own handler runs every rule whenever an endpoint is validated, so an endpoint saved in
/// the editor and one imported by a recipe are held to the same rules.
/// </summary>
public interface IChannelEndpointRule
{
    /// <summary>
    /// Validates a channel endpoint, failing <paramref name="context"/> for anything this rule refuses.
    /// </summary>
    /// <param name="context">The validation context carrying the endpoint and its result.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the endpoint has been checked.</returns>
    Task ValidateAsync(ValidatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default);
}
