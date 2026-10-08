using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Keeps each agent on one outbound line. A phone number whose line names an agent already on another number's line
/// is refused, whether it is saved from the editor or imported by a recipe, so the number an agent dials from is
/// never ambiguous.
/// </summary>
internal sealed class OutboundLineEndpointRule : IChannelEndpointRule
{
    private readonly IOmnichannelChannelEndpointStore _endpointStore;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundLineEndpointRule"/> class.
    /// </summary>
    /// <param name="endpointStore">The store of the tenant's numbers, read directly because the catalog manager runs this rule.</param>
    /// <param name="userManager">The Orchard user manager.</param>
    /// <param name="displayNameProvider">The provider of the names users are shown by.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OutboundLineEndpointRule(
        IOmnichannelChannelEndpointStore endpointStore,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IStringLocalizer<OutboundLineEndpointRule> stringLocalizer)
    {
        _endpointStore = endpointStore;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task ValidateAsync(ValidatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        var endpoint = context.Model;

        if (!ChannelEndpointOutboundLineResolver.IsLine(endpoint))
        {
            return;
        }

        var userIds = ChannelEndpointOutboundLineResolver.GetUserIds(endpoint);

        if (userIds.Count == 0)
        {
            return;
        }

        var endpoints = await _endpointStore.GetAllAsync(cancellationToken);

        foreach (var userId in userIds.Distinct(StringComparer.Ordinal))
        {
            var otherLine = ChannelEndpointOutboundLineResolver.FindAssignedLine(endpoints, userId, excludeEndpointId: endpoint.ItemId);

            if (otherLine is not null)
            {
                context.Result.Fail(new ValidationResult(
                    S["{0} already dials out from {1}. Remove them from that number's line first.", await GetUserNameAsync(userId), GetLineName(otherLine)],
                    [nameof(OutboundLineSettings.UserIds)]));
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
