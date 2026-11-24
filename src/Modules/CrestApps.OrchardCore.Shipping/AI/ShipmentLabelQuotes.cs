using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Keeps the carrier rates <c>getShipmentLabelRates</c> was quoted for a shipment, for 30 minutes, so
/// <c>buyShipmentLabel</c> buys only a rate that was quoted for that shipment, at the price the person approved.
/// </summary>
internal static class ShipmentLabelQuotes
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Keeps a quote.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="quote">The quote.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="false"/> when the site has no cache to keep it in.</returns>
    public static async Task<bool> KeepAsync(IServiceProvider services, QuotedLabel quote, CancellationToken cancellationToken)
    {
        if (services.GetService<IDistributedCache>() is not { } cache)
        {
            return false;
        }

        await cache.SetStringAsync(GetKey(services, quote.QuoteId), JsonSerializer.Serialize(quote), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _lifetime,
        }, cancellationToken);

        return true;
    }

    /// <summary>
    /// Finds a quote kept earlier.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="quoteId">The quote's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The quote, or <see langword="null"/> when it was not kept or has expired.</returns>
    public static async Task<QuotedLabel> FindAsync(IServiceProvider services, string quoteId, CancellationToken cancellationToken)
    {
        if (services.GetService<IDistributedCache>() is not { } cache ||
            await cache.GetStringAsync(GetKey(services, quoteId), cancellationToken) is not { Length: > 0 } json)
        {
            return null;
        }

        return JsonSerializer.Deserialize<QuotedLabel>(json);
    }

    private static string GetKey(IServiceProvider services, string quoteId)
        => $"{services.GetService<ShellSettings>()?.Name}|ai-tool:shipment-label-quote|{quoteId}";
}

/// <summary>
/// A carrier quote for one shipment's label.
/// </summary>
internal sealed class QuotedLabel
{
    /// <summary>
    /// Gets or sets the carrier's quote id.
    /// </summary>
    public string QuoteId { get; set; }

    /// <summary>
    /// Gets or sets the shipment the quote is for.
    /// </summary>
    public string ShipmentId { get; set; }

    /// <summary>
    /// Gets or sets the rates quoted.
    /// </summary>
    public List<QuotedLabelRate> Rates { get; set; } = [];
}

/// <summary>
/// One rate of a carrier quote.
/// </summary>
internal sealed class QuotedLabelRate
{
    /// <summary>
    /// Gets or sets the rate's id.
    /// </summary>
    public string RateId { get; set; }

    /// <summary>
    /// Gets or sets the carrier.
    /// </summary>
    public string Carrier { get; set; }

    /// <summary>
    /// Gets or sets the carrier's service.
    /// </summary>
    public string Service { get; set; }

    /// <summary>
    /// Gets or sets the price.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the price's currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the days to deliver, when the carrier says.
    /// </summary>
    public int? DeliveryDays { get; set; }
}
