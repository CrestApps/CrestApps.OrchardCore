using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Names the numbers agents dial out from. Each is a number the tenant owns, so a transfer to one would only ring
/// the contact center back.
/// </summary>
public sealed class OutboundLineOwnNumberSource : IContactCenterOwnNumberSource
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundLineOwnNumberSource"/> class.
    /// </summary>
    /// <param name="endpointManager">The channel endpoint catalog holding the tenant's numbers.</param>
    public OutboundLineOwnNumberSource(IOmnichannelChannelEndpointManager endpointManager)
    {
        _endpointManager = endpointManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetOwnNumbersAsync(CancellationToken cancellationToken = default)
    {
        var endpoints = await _endpointManager.GetAllAsync(cancellationToken);

        return endpoints
            .Where(ChannelEndpointOutboundLineResolver.IsLine)
            .Select(endpoint => endpoint.Value)
            .ToArray();
    }
}
