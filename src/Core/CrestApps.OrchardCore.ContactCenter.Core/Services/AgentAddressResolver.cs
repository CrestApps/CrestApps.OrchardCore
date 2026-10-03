using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentAddressResolver"/> over the Omnichannel Addresses and the
/// default numbers chosen under Settings &gt; Contact Center.
/// </summary>
public sealed class AgentAddressResolver : IAgentAddressResolver
{
    private readonly IOmnichannelChannelEndpointManager _addressManager;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAddressResolver"/> class.
    /// </summary>
    /// <param name="addressManager">The address list.</param>
    /// <param name="siteService">The site settings, which hold the default numbers.</param>
    public AgentAddressResolver(IOmnichannelChannelEndpointManager addressManager, ISiteService siteService)
    {
        _addressManager = addressManager;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<AgentAddresses> ResolveAsync(string userId, CancellationToken cancellationToken = default)
    {
        var addresses = (await _addressManager.GetAllAsync(cancellationToken)).ToList();
        var defaults = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<ContactCenterDefaultAddressSettings>();

        var phone = ChannelEndpointOutboundLineResolver.FindAssignedLine(addresses, userId);
        var sms = MessagingLines.FindAssignedLine(addresses, userId, OmnichannelConstants.Channels.Sms);

        var defaultPhone = phone is null ? FindDefault(addresses, defaults.DefaultPhoneAddressId, OmnichannelConstants.Channels.Phone) : null;
        var defaultSms = sms is null ? FindDefault(addresses, defaults.DefaultSmsAddressId, OmnichannelConstants.Channels.Sms) : null;

        return new AgentAddresses
        {
            PhoneAddress = phone ?? defaultPhone,
            IsDefaultPhone = phone is null && defaultPhone is not null,
            SmsAddress = sms ?? defaultSms,
            IsDefaultSms = sms is null && defaultSms is not null,
        };
    }

    // A default that was merged into another address, or is no longer used for its channel, is not offered.
    private static OmnichannelChannelEndpoint FindDefault(IEnumerable<OmnichannelChannelEndpoint> addresses, string addressId, string channel)
        => string.IsNullOrWhiteSpace(addressId)
            ? null
            : addresses.FirstOrDefault(address => address.IsKnownAs(addressId) && address.HasCapability(channel) && !string.IsNullOrWhiteSpace(address.Value));
}
