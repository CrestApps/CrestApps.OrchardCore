using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Shows which shipping zone a country and region fall in and what each of its methods would charge for a test parcel,
/// as the Try your rates page of Commerce › Shipping builds it (<see cref="IShippingRateService.FindZoneAsync"/> and
/// <see cref="IShippingRateService.GetRatesAsync"/>), without placing an order. With carrier rates it asks the carrier
/// for a live quote.
/// </summary>
internal sealed class TestShippingRatesTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "testShippingRates";

    private const int MaxItems = 10_000;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "country": {
          "type": "string",
          "description": "The two-letter country code, such as US."
        },
        "region": {
          "type": "string",
          "description": "The region or state code, such as CA."
        },
        "items": {
          "type": "integer",
          "description": "How many items are in the parcel, 1 by default."
        },
        "subtotal": {
          "type": "number",
          "description": "What the items cost together."
        },
        "weight": {
          "type": "number",
          "description": "What the items weigh together, in the shipping settings' weight unit."
        },
        "shippingClass": {
          "type": "string",
          "description": "A shipping class of the shipping settings, when the items have one."
        },
        "currency": {
          "type": "string",
          "description": "The currency, such as USD; the store's first currency by default."
        }
      },
      "required": ["country"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Shows which shipping zone a country and region fall in and what each of its methods would charge for a test parcel of a given item count, subtotal, weight and shipping class, without placing an order. Use it to check rates after changing zones or methods; with carrier rates it asks the carrier for a live quote.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ShippingPermissions.ManageShipping;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var country = ReadString(arguments, "country")?.ToUpperInvariant();
        var countries = await services.GetRequiredService<ICountryService>().GetCountriesAsync();

        if (country is null || !countries.Any(entry => string.Equals(entry.Code, country, StringComparison.OrdinalIgnoreCase)))
        {
            return "Nothing was priced: give a two-letter country code the site knows, such as US.";
        }

        if (!TryReadDecimal(arguments, "subtotal", out var subtotal) || !TryReadDecimal(arguments, "weight", out var weight) || !TryReadDecimal(arguments, "items", out var itemCount))
        {
            return "Nothing was priced: items, subtotal and weight must be numbers.";
        }

        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<ShippingSettings>();
        var shippingClass = ReadString(arguments, "shippingClass");

        if (shippingClass is not null && !(settings.ShippingClasses ?? []).Contains(shippingClass, StringComparer.OrdinalIgnoreCase))
        {
            return "Nothing was priced: that shipping class is not one of the shipping settings' classes.";
        }

        var currency = ReadString(arguments, "currency")?.ToUpperInvariant();

        if (currency is null)
        {
            var currencies = await services.GetRequiredService<IProductCurrencyProvider>().GetCurrenciesAsync(cancellationToken);
            currency = currencies.Count > 0 ? currencies[0].CurrencyCode : null;
        }

        // As the page: the item count is clamped to 1 to 10,000, and negative amounts count as zero.
        var items = (int)Math.Clamp(itemCount ?? 1m, 1m, MaxItems);
        var region = ReadString(arguments, "region")?.ToUpperInvariant();
        var request = new ShippingRateRequest
        {
            Destination = new Address { Country = country, Region = region },
            Currency = currency,
            Lines =
            [
                new ShippingRequestLine
                {
                    Title = "Test item",
                    Quantity = items,
                    UnitPrice = Math.Max(0, subtotal ?? 0m) / items,
                    Weight = Math.Max(0, weight ?? 0m) / items,
                    ShippingClass = shippingClass,
                },
            ],
        };

        var rateService = services.GetRequiredService<IShippingRateService>();
        var zone = await rateService.FindZoneAsync(request.Destination, cancellationToken);
        var rates = await rateService.GetRatesAsync(request, cancellationToken);

        return Json(new
        {
            country,
            region,
            currency,
            weightUnit = ToCamel(settings.WeightUnit.ToString()),
            zone = zone is null ? null : new { id = zone.ItemId, name = zone.Name },
            rates = rates.Select(rate => new
            {
                methodId = rate.MethodId,
                name = rate.Name,
                description = rate.Description,
                provider = rate.ProviderName,
                amount = rate.Amount,
                currency = rate.Currency,
            }),
            message = zone is null
                ? "No shipping zone covers this address, so it can't be shipped to."
                : rates.Count == 0
                    ? $"The address falls in the zone {zone.Name}, but none of its methods offers a rate for this parcel."
                    : $"The address falls in the zone {zone.Name}.",
        });
    }
}
