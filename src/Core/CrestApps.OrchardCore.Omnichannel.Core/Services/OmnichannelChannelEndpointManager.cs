using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IOmnichannelChannelEndpointManager"/> that delegates storage
/// to <see cref="IOmnichannelChannelEndpointStore"/> and loads entries through catalog handlers.
/// </summary>
public sealed class OmnichannelChannelEndpointManager : CatalogManager<OmnichannelChannelEndpoint>, IOmnichannelChannelEndpointManager
{
    private readonly IOmnichannelChannelEndpointStore _store;
    private readonly IPhoneNumberService _phoneNumberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointManager"/> class.
    /// </summary>
    /// <param name="store">The underlying channel endpoint store.</param>
    /// <param name="phoneNumberService">The phone number service used to canonicalize addresses for matching.</param>
    /// <param name="handlers">The catalog entry handlers for channel endpoint entries.</param>
    /// <param name="logger">The logger instance.</param>
    public OmnichannelChannelEndpointManager(
        IOmnichannelChannelEndpointStore store,
        IPhoneNumberService phoneNumberService,
        IEnumerable<ICatalogEntryHandler<OmnichannelChannelEndpoint>> handlers,
        ILogger<CatalogManager<OmnichannelChannelEndpoint>> logger)
    : base(store, handlers, logger)
    {
        _store = store;
        _phoneNumberService = phoneNumberService;
    }

    /// <inheritdoc/>
    public ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress, CancellationToken cancellationToken = default)
    {
        // A number endpoint stores its value in canonical E.164 form on save. Inbound traffic can arrive written
        // differently (national format, a missing "+", spaces), so canonicalize the query the same way before the
        // exact-match lookup; otherwise a validly-configured endpoint silently fails to match its own inbound.
        if (!string.IsNullOrEmpty(serviceAddress) &&
            (string.Equals(channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase)) &&
            _phoneNumberService.TryParse(serviceAddress, out var canonical))
        {
            serviceAddress = canonical.Value;
        }

        return _store.GetByServiceAddressAsync(channel, serviceAddress, cancellationToken);
    }
}
