using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Picks the address a new conversation starts from for the signed-in agent: on SMS their own number or the
/// tenant-wide default, and on every other channel the address they were made a messaging line of.
/// </summary>
public sealed class MessagingSenderLines
{
    private readonly IAgentAddressResolver _agentAddressResolver;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;

    public MessagingSenderLines(
        IAgentAddressResolver agentAddressResolver,
        IOmnichannelChannelEndpointManager endpointManager)
    {
        _agentAddressResolver = agentAddressResolver;
        _endpointManager = endpointManager;
    }

    /// <summary>
    /// Returns the id of the address <paramref name="user"/> sends from on <paramref name="channel"/>, or
    /// <see langword="null"/> when they have none there.
    /// </summary>
    public async Task<string> FindAddressIdAsync(ClaimsPrincipal user, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        // Texts have a tenant-wide default number besides the agent's own line. Every other channel starts from the
        // agent's line on it, the address they were made a messaging line of, when they have one.
        if (string.IsNullOrEmpty(channel) || string.Equals(channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase))
        {
            var addresses = await _agentAddressResolver.ResolveAsync(userId, cancellationToken);

            return addresses.SmsAddress?.ItemId;
        }

        var endpoints = await _endpointManager.GetAllAsync(cancellationToken);

        return MessagingLines.FindAssignedLine(endpoints, userId, channel)?.ItemId;
    }
}
