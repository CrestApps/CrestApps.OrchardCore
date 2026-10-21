using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Taxation.TaxJar;

/// <summary>
/// Works out tax with TaxJar for orders to the countries it's set up for, in place of the store's own rules: what to
/// collect for each line and for shipping, at the combined rate of the destination's jurisdictions. Prices that include
/// tax are left to the store's rules, since TaxJar adds tax to a price.
/// </summary>
public sealed class TaxJarTaxDeterminationProvider : ITaxDeterminationProvider
{
    public const string HttpClientName = "TaxJar";

    public const string CalculationMethod = "TaxJar";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IOptionsMonitor<TaxJarOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public TaxJarTaxDeterminationProvider(
        IOptionsMonitor<TaxJarOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    // Its place among other tax providers; the first that can handle an order works out its tax.
    public int Order
        => 100;

    public bool CanHandle(TaxCalculationContext context)
    {
        var options = _options.CurrentValue;

        return context is not null &&
            !string.IsNullOrEmpty(options.ApiKey) &&
            context.DefaultPriceType == TaxPriceType.Exclusive &&
            context.Destination?.Country is { Length: 2 } country &&
            options.Countries.Contains(country, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<TaxCalculationResult> DetermineAsync(TaxCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = _options.CurrentValue;
        var request = BuildRequest(context);

        using var message = new HttpRequestMessage(HttpMethod.Post, options.BaseUrl + "taxes")
        {
            Content = JsonContent.Create(request, options: _json),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        TaxJarResponse answer;

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);

                throw new InvalidOperationException($"TaxJar couldn't work out the tax ({(int)response.StatusCode}): {detail[..Math.Min(detail.Length, 300)]}");
            }

            answer = await response.Content.ReadFromJsonAsync<TaxJarResponse>(_json, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // The tax service logs the failure and works the tax out by the store's own rules.
            throw new InvalidOperationException("TaxJar couldn't be reached to work out the tax.", exception);
        }

        return ToResult(context, answer?.Tax);
    }

    /// <summary>
    /// Returns the request TaxJar is asked: where the order ships from and to, its lines and shipping.
    /// </summary>
    internal static TaxJarRequest BuildRequest(TaxCalculationContext context)
    {
        var shipping = context.Items.Where(item => item.Kind == TaxableItemKind.Shipping).ToList();
        var lines = context.Items.Where(item => item.Kind != TaxableItemKind.Shipping).ToList();

        return new TaxJarRequest
        {
            FromCountry = context.Origin?.Country,
            FromZip = context.Origin?.PostalCode,
            FromState = context.Origin?.Region,
            FromCity = context.Origin?.City,
            FromStreet = context.Origin?.AddressLine1,
            ToCountry = context.Destination.Country,
            ToZip = context.Destination.PostalCode,
            ToState = context.Destination.Region,
            ToCity = context.Destination.City,
            ToStreet = context.Destination.AddressLine1,
            Amount = lines.Sum(item => (item.UnitPrice * item.Quantity) - item.DiscountAmount),
            Shipping = shipping.Sum(item => (item.UnitPrice * item.Quantity) - item.DiscountAmount),
            CustomerExemptionType = context.Customer?.IsTaxExempt == true ? "other" : null,
            LineItems = [.. lines.Select(item => new TaxJarLineItem
            {
                Id = item.Id,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.DiscountAmount,
                ProductTaxCode = string.IsNullOrWhiteSpace(item.TaxClassificationCode) ? null : item.TaxClassificationCode,
            })],
        };
    }

    /// <summary>
    /// Returns TaxJar's answer as the store's tax result: a line for each line and the shipping that owes tax.
    /// </summary>
    internal static TaxCalculationResult ToResult(TaxCalculationContext context, TaxJarTax tax)
    {
        var result = new TaxCalculationResult { Currency = context.Currency };
        var nominal = context.Items.Sum(item => (item.UnitPrice * item.Quantity) - item.DiscountAmount);

        if (tax is null)
        {
            result.TotalAmount = nominal;

            return result;
        }

        var isVat = !string.Equals(context.Destination?.Country, "US", StringComparison.OrdinalIgnoreCase);
        var taxType = isVat ? TaxTypeNames.Vat : TaxTypeNames.SalesTax;
        var taxName = isVat ? "VAT" : "Sales tax";
        var jurisdiction = string.Join(", ", new[] { tax.Jurisdictions?.City, tax.Jurisdictions?.County, tax.Jurisdictions?.State, tax.Jurisdictions?.Country }.Where(part => !string.IsNullOrWhiteSpace(part)));

        foreach (var line in tax.Breakdown?.LineItems ?? [])
        {
            if (line.TaxCollectable == 0m)
            {
                continue;
            }

            result.Lines.Add(new TaxLine
            {
                ItemId = line.Id,
                TaxName = taxName,
                TaxType = taxType,
                JurisdictionName = jurisdiction,
                Rate = line.CombinedTaxRate,
                TaxableAmount = line.TaxableAmount,
                TaxAmount = line.TaxCollectable,
                CalculationMethod = CalculationMethod,
            });
        }

        var shippingTax = tax.Breakdown?.Shipping;

        if (shippingTax is { TaxCollectable: not 0m } &&
            context.Items.FirstOrDefault(item => item.Kind == TaxableItemKind.Shipping) is { } shipping)
        {
            result.Lines.Add(new TaxLine
            {
                ItemId = shipping.Id,
                TaxName = taxName,
                TaxType = taxType,
                JurisdictionName = jurisdiction,
                Rate = shippingTax.CombinedTaxRate,
                TaxableAmount = shippingTax.TaxableAmount,
                TaxAmount = shippingTax.TaxCollectable,
                CalculationMethod = CalculationMethod,
            });
        }

        result.TaxableAmount = tax.TaxableAmount;
        result.TaxAmount = tax.AmountToCollect;
        result.TotalAmount = nominal + tax.AmountToCollect;

        return result;
    }

    internal sealed class TaxJarRequest
    {
        public string FromCountry { get; set; }

        public string FromZip { get; set; }

        public string FromState { get; set; }

        public string FromCity { get; set; }

        public string FromStreet { get; set; }

        public string ToCountry { get; set; }

        public string ToZip { get; set; }

        public string ToState { get; set; }

        public string ToCity { get; set; }

        public string ToStreet { get; set; }

        public decimal Amount { get; set; }

        public decimal Shipping { get; set; }

        public string CustomerExemptionType { get; set; }

        public List<TaxJarLineItem> LineItems { get; set; } = [];
    }

    internal sealed class TaxJarLineItem
    {
        public string Id { get; set; }

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal Discount { get; set; }

        public string ProductTaxCode { get; set; }
    }

    internal sealed class TaxJarResponse
    {
        public TaxJarTax Tax { get; set; }
    }

    internal sealed class TaxJarTax
    {
        public decimal TaxableAmount { get; set; }

        public decimal AmountToCollect { get; set; }

        public TaxJarJurisdictions Jurisdictions { get; set; }

        public TaxJarBreakdown Breakdown { get; set; }
    }

    internal sealed class TaxJarJurisdictions
    {
        public string Country { get; set; }

        public string State { get; set; }

        public string County { get; set; }

        public string City { get; set; }
    }

    internal sealed class TaxJarBreakdown
    {
        public TaxJarLineTax Shipping { get; set; }

        public List<TaxJarLineTax> LineItems { get; set; }
    }

    internal sealed class TaxJarLineTax
    {
        public string Id { get; set; }

        public decimal TaxableAmount { get; set; }

        public decimal TaxCollectable { get; set; }

        public decimal CombinedTaxRate { get; set; }
    }
}
