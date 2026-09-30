using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves a user's outbound line from the tenant's phone channel endpoints: the number whose line the user is
/// assigned to is the caller ID their calls present.
/// </summary>
public sealed class ChannelEndpointOutboundLineResolver : IOutboundLineResolver
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelEndpointOutboundLineResolver"/> class.
    /// </summary>
    /// <param name="endpointManager">The channel endpoint catalog holding the tenant's numbers.</param>
    public ChannelEndpointOutboundLineResolver(IOmnichannelChannelEndpointManager endpointManager)
    {
        _endpointManager = endpointManager;
    }

    /// <inheritdoc/>
    public async Task<OutboundLine> ResolveAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var endpoints = await _endpointManager.GetAllAsync(cancellationToken);
        var endpoint = FindAssignedLine(endpoints, userId);

        if (endpoint is null)
        {
            return null;
        }

        return new OutboundLine
        {
            Id = endpoint.ItemId,
            Name = string.IsNullOrWhiteSpace(endpoint.DisplayText) ? endpoint.Value : endpoint.DisplayText,
            Number = endpoint.Value,
        };
    }

    /// <summary>
    /// Finds the phone number whose line a user is assigned to.
    /// </summary>
    /// <param name="endpoints">The tenant's channel endpoints.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="excludeEndpointId">An endpoint to leave out, such as the one being edited.</param>
    /// <returns>The endpoint, or <see langword="null"/> when the user has no line.</returns>
    /// <remarks>
    /// A user is kept on one line when lines are saved. Should two lines still name the same user (an import, or two
    /// saves racing), the earliest-created one wins, so the answer is the same on every call.
    /// </remarks>
    public static OmnichannelChannelEndpoint FindAssignedLine(IEnumerable<OmnichannelChannelEndpoint> endpoints, string userId, string excludeEndpointId = null)
    {
        if (endpoints is null || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return endpoints
            .Where(endpoint => IsLine(endpoint) &&
                !string.Equals(endpoint.ItemId, excludeEndpointId, StringComparison.Ordinal) &&
                GetUserIds(endpoint).Contains(userId, StringComparer.Ordinal))
            .OrderBy(endpoint => endpoint.CreatedUtc)
            .ThenBy(endpoint => endpoint.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the users assigned to a phone number's line.
    /// </summary>
    /// <param name="endpoint">The channel endpoint.</param>
    /// <returns>The user identifiers, possibly empty.</returns>
    public static IReadOnlyList<string> GetUserIds(OmnichannelChannelEndpoint endpoint)
    {
        if (endpoint is null || !endpoint.TryGet<OutboundLineSettings>(out var settings) || settings?.UserIds is null)
        {
            return [];
        }

        return settings.UserIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .ToArray();
    }

    /// <summary>
    /// Gets a value indicating whether a channel endpoint is a phone number that can be dialed out from.
    /// </summary>
    /// <param name="endpoint">The channel endpoint.</param>
    /// <returns><see langword="true"/> for a phone endpoint with a number.</returns>
    public static bool IsLine(OmnichannelChannelEndpoint endpoint)
        => endpoint is not null &&
            string.Equals(endpoint.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(endpoint.Value);
}
