using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Binds the rates quoted on a shipment's Buy label page to that shipment. The quote's id, its rates, the carrier
/// provider and the addresses it was quoted for go back to the page in a signed token that lasts an hour, and a label is
/// bought only for a quote and rate that the token names, for the same shipment, provider and addresses.
/// </summary>
/// <remarks>
/// The quote id is the carrier account's own id, such as an EasyPost shipment id, so without the token a form could
/// buy another shipment's quote, an old one, or one quoted to the order's previous address.
/// </remarks>
internal static class ShipmentLabelQuotes
{
    /// <summary>
    /// The purpose the token is protected for.
    /// </summary>
    public const string Purpose = "Shipping.LabelQuote";

    /// <summary>
    /// How long quoted rates can be bought from; after that the page asks to compare them again.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Returns the token that binds a quote to the shipment it was made for.
    /// </summary>
    public static string Protect(SignedTokenService tokens, Shipment shipment, ICarrierProvider carrier, CarrierQuote quote, Address from, Address to)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(shipment);
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(quote);

        var binding = new JsonObject
        {
            ["shipment"] = shipment.ItemId,
            ["provider"] = carrier.Name,
            ["quote"] = quote.QuoteId,
            ["rates"] = new JsonArray([.. quote.Rates.Select(rate => JsonValue.Create(rate.RateId))]),
            ["addresses"] = Fingerprint(from, to),
        };

        return tokens.Protect(Purpose, binding.ToJsonString(), Lifetime);
    }

    /// <summary>
    /// Returns whether the token was made, less than <see cref="Lifetime"/> ago, for this shipment's quote and one of its
    /// rates, with the same carrier provider and addresses.
    /// </summary>
    public static bool IsFor(SignedTokenService tokens, string token, Shipment shipment, ICarrierProvider carrier, string quoteId, string rateId, Address from, Address to)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(shipment);
        ArgumentNullException.ThrowIfNull(carrier);

        if (string.IsNullOrEmpty(quoteId) || string.IsNullOrEmpty(rateId) || tokens.Unprotect(Purpose, token) is not { } json ||
            JsonNode.Parse(json) is not JsonObject binding)
        {
            return false;
        }

        return string.Equals(binding["shipment"]?.GetValue<string>(), shipment.ItemId, StringComparison.Ordinal) &&
            string.Equals(binding["provider"]?.GetValue<string>(), carrier.Name, StringComparison.Ordinal) &&
            string.Equals(binding["quote"]?.GetValue<string>(), quoteId, StringComparison.Ordinal) &&
            binding["rates"] is JsonArray rates && rates.Any(rate => string.Equals(rate?.GetValue<string>(), rateId, StringComparison.Ordinal)) &&
            string.Equals(binding["addresses"]?.GetValue<string>(), Fingerprint(from, to), StringComparison.Ordinal);
    }

    // The parts of the addresses a label is printed with; a geocode added later doesn't change where the parcel goes.
    private static string Fingerprint(Address from, Address to)
    {
        var text = JsonSerializer.Serialize(new[] { Lines(from), Lines(to) });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static string[] Lines(Address address)
        => address is null
            ? []
            : [address.Name, address.Company, address.AddressLine1, address.AddressLine2, address.DependentLocality, address.City, address.District, address.County, address.Region, address.PostalCode, address.SortingCode, address.Country, address.Phone];
}
