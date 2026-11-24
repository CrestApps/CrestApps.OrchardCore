using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Buys a number from the Telnyx account and adds it as a phone address of the site, as the Telnyx numbers page's Buy
/// button does. The order is never retried: a repeated order buys the number twice.
/// </summary>
internal static class TelnyxNumberPurchase
{
    /// <summary>
    /// Orders the number and, once Telnyx accepts the order, adds it as a phone address when the address catalog is
    /// available and the new address is valid.
    /// </summary>
    /// <param name="client">The Telnyx API client.</param>
    /// <param name="endpoints">The address catalog, or <see langword="null"/> when Omnichannel addresses are off.</param>
    /// <param name="phoneNumber">The number, in E.164 form, as a search returned it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The order's outcome and the address, when one was added.</returns>
    public static async Task<TelnyxNumberPurchaseResult> BuyAsync(
        TelnyxApiClient client,
        ICatalogManager<OmnichannelChannelEndpoint> endpoints,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var (result, orderId, status) = await client.OrderNumbersAsync([phoneNumber], cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            return new TelnyxNumberPurchaseResult(result, orderId, status, Address: null);
        }

        OmnichannelChannelEndpoint address = null;

        if (endpoints is not null)
        {
            var endpoint = await endpoints.NewAsync(cancellationToken: cancellationToken);
            endpoint.DisplayText = phoneNumber;
            endpoint.Value = phoneNumber;
            endpoint.Channel = OmnichannelConstants.Channels.Phone;
            endpoint.Capabilities = [OmnichannelConstants.Channels.Phone];
            endpoint.ProviderName = TelnyxConstants.ProviderTechnicalName;

            if ((await endpoints.ValidateAsync(endpoint, cancellationToken)).Succeeded)
            {
                await endpoints.CreateAsync(endpoint, cancellationToken);
                address = endpoint;
            }
        }

        return new TelnyxNumberPurchaseResult(result, orderId, status, address);
    }
}

/// <summary>
/// The outcome of buying a number.
/// </summary>
/// <param name="Result">Telnyx's answer to the order.</param>
/// <param name="OrderId">The order's id.</param>
/// <param name="Status">The order's status, often pending at first.</param>
/// <param name="Address">The phone address added for the number, or <see langword="null"/> when none was.</param>
internal sealed record TelnyxNumberPurchaseResult(TelnyxApiResult Result, string OrderId, string Status, OmnichannelChannelEndpoint Address);
