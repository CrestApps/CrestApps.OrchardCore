using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Keeps each agent on one texting number per channel. A number whose texting line names an agent already on another
/// number's line for the same channel is refused, whether it is saved from the editor or imported by a recipe, so the
/// number an agent texts from is never ambiguous.
/// </summary>
internal sealed class MessagingLineEndpointRule : IChannelEndpointRule
{
    private readonly IOmnichannelChannelEndpointStore _endpointStore;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingLineEndpointRule"/> class.
    /// </summary>
    /// <param name="endpointStore">The store of the tenant's numbers, read directly because the catalog manager runs this rule.</param>
    /// <param name="channelResolver">The enabled messaging channels.</param>
    /// <param name="userManager">The Orchard user manager.</param>
    /// <param name="displayNameProvider">The provider of the names users are shown by.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingLineEndpointRule(
        IOmnichannelChannelEndpointStore endpointStore,
        IMessagingChannelResolver channelResolver,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IStringLocalizer<MessagingLineEndpointRule> stringLocalizer)
    {
        _endpointStore = endpointStore;
        _channelResolver = channelResolver;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task ValidateAsync(ValidatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        var endpoint = context.Model;
        var userIds = MessagingLines.GetUserIds(endpoint);

        if (userIds.Count == 0)
        {
            return;
        }

        var channels = _channelResolver.GetAll()
            .Select(channel => channel.Name)
            .Where(endpoint.HasCapability)
            .ToList();

        if (channels.Count == 0)
        {
            return;
        }

        var endpoints = await _endpointStore.GetAllAsync(cancellationToken);

        foreach (var userId in userIds.Distinct(StringComparer.Ordinal))
        {
            foreach (var channel in channels)
            {
                var otherLine = MessagingLines.FindAssignedLine(endpoints, userId, channel, excludeAddressId: endpoint.ItemId);

                if (otherLine is not null)
                {
                    context.Result.Fail(new ValidationResult(
                        S["{0} already texts from {1}. Remove them from that number's list first.", await GetUserNameAsync(userId), GetLineName(otherLine)],
                        ["TextingUserIds"]));

                    break;
                }
            }
        }
    }

    private async Task<string> GetUserNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return userId;
        }

        var name = await _displayNameProvider.GetAsync(user);

        return string.IsNullOrWhiteSpace(name) ? userId : name;
    }

    private static string GetLineName(OmnichannelChannelEndpoint endpoint)
        => string.IsNullOrWhiteSpace(endpoint.DisplayText)
            ? endpoint.Value
            : $"{endpoint.DisplayText} ({endpoint.Value})";
}
