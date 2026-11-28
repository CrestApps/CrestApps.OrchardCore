using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Changes the price fields of one product, or of one of its variants, with the product editor's checks
/// (<see cref="ProductPricingRules"/> for the product, <see cref="ProductVariantRules.IsValidPrice"/> for a variant). The
/// fields given are merged onto the stored ones first, so a new sale price is compared with the stored price. Sale dates
/// are taken in the store's time zone and stored in UTC, as the editor does; a variant has no sale dates of its own. Every
/// amount must fit its currency's decimal places (<c>CurrencyScale</c>). It needs <c>EditContent</c> on the product, and
/// <c>PublishContent</c> to publish.
/// </summary>
internal sealed class UpdateProductPricingTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "updateProductPricing";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The product's content item id, from listProducts."
        },
        "variantId": {
          "type": "string",
          "description": "The variant's id, to change one variant's prices instead of the product's. A variant has no sale dates of its own: its sale price applies during the product's sale dates."
        },
        "price": {
          "type": ["number", "null"],
          "description": "The price, in the product's currency. null clears a variant's own price, so it sells at the product's price."
        },
        "compareAtPrice": {
          "type": ["number", "null"],
          "description": "The price shown struck through next to the price. null clears it."
        },
        "salePrice": {
          "type": ["number", "null"],
          "description": "The sale price, lower than the price. null ends the sale."
        },
        "saleStarts": {
          "type": ["string", "null"],
          "description": "When the product's sale starts, as yyyy-MM-ddTHH:mm in the store's time zone. null to start it at once. Product only."
        },
        "saleEnds": {
          "type": ["string", "null"],
          "description": "When the product's sale ends, as yyyy-MM-ddTHH:mm in the store's time zone, after it starts. null to run it with no end. Product only."
        },
        "publish": {
          "type": "boolean",
          "description": "true to publish the change at once, which needs the permission to publish the product and is refused while the product has other unpublished changes. false or left out to save it in a draft, which the shop shows only once it is published."
        }
      },
      "required": ["contentItemId"],
      "additionalProperties": false
    }
    """);

    private static readonly string[] _priceFields = ["price", "compareAtPrice", "salePrice"];

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Changes the price fields of one product, or of one of its variants: price, compare-at price, sale price and the sale's start and end in the site's time zone, with the same checks as the product editor. Use it for price changes and scheduled sales; use the content tools for everything else on a product. Saves a draft unless asked to publish.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => null;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (latest, type, error) = await LoadProductAsync(services, ReadString(arguments, "contentItemId"));

        if (latest is null)
        {
            return error;
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.EditContent, latest))
        {
            return NotAllowedMessage;
        }

        if (!_priceFields.Any(field => Has(arguments, field)) && !Has(arguments, "saleStarts") && !Has(arguments, "saleEnds"))
        {
            return "Nothing was changed: give at least one of price, compareAtPrice, salePrice, saleStarts or saleEnds.";
        }

        var product = GetPart<ProductPart>(latest) ?? new ProductPart();
        var currency = GetCurrency(product, type);
        var amounts = new Dictionary<string, decimal?>(StringComparer.Ordinal);

        foreach (var field in _priceFields.Where(field => Has(arguments, field)))
        {
            if (ReadJson(arguments, field) is not { } element)
            {
                amounts[field] = null;

                continue;
            }

            if (!TryReadDecimal(element, out var amount))
            {
                return $"Nothing was changed: {field} must be a number, such as 19.99.";
            }

            if (CheckAmount(field, amount, currency) is { } amountError)
            {
                return amountError;
            }

            amounts[field] = amount;
        }

        var variantId = ReadString(arguments, "variantId");

        return variantId is null
            ? await UpdateProductAsync(arguments, services, caller, latest, type, product, currency, amounts)
            : await UpdateVariantAsync(arguments, services, caller, latest, product, currency, variantId, amounts);
    }

    private static async Task<object> UpdateProductAsync(
        AIFunctionArguments arguments,
        IServiceProvider services,
        ClaimsPrincipal caller,
        ContentItem latest,
        ContentTypeDefinition type,
        ProductPart stored,
        string currency,
        Dictionary<string, decimal?> amounts)
    {
        if (amounts.TryGetValue("price", out var newPrice) && newPrice is null)
        {
            // A product's price is cleared only for a product sold by inquiry, which the editor decides with its type.
            if (stored.RequiresPrice(GetProductSettings(type)) && !HasGroupedProducts(latest))
            {
                return "Nothing was changed: Price is required for a product that can be bought or shows its price.";
            }
        }

        var price = amounts.TryGetValue("price", out var p) ? p : stored.Price;
        var compareAtPrice = amounts.TryGetValue("compareAtPrice", out var c) ? c : stored.CompareAtPrice;
        var salePrice = amounts.TryGetValue("salePrice", out var s) ? s : stored.SalePrice;
        var saleStartsUtc = stored.SaleStartsUtc;
        var saleEndsUtc = stored.SaleEndsUtc;

        if (Has(arguments, "saleStarts"))
        {
            var (utc, error) = await ReadLocalTimeAsync(services, arguments, "saleStarts");

            if (error is not null)
            {
                return error;
            }

            saleStartsUtc = utc;
        }

        if (Has(arguments, "saleEnds"))
        {
            var (utc, error) = await ReadLocalTimeAsync(services, arguments, "saleEnds");

            if (error is not null)
            {
                return error;
            }

            saleEndsUtc = utc;
        }

        var priceRequired = stored.RequiresPrice(GetProductSettings(type)) && !HasGroupedProducts(latest);
        var problems = ProductPricingRules.Check(price, priceRequired, compareAtPrice, salePrice, saleStartsUtc, saleEndsUtc);

        if (problems.Count > 0)
        {
            return "Nothing was changed: " + string.Join(" ", problems.Select(problem => problem switch
            {
                ProductPriceProblem.PriceRequired => "Price is required for a product that can be bought or shows its price.",
                ProductPriceProblem.NegativePrice => "The price cannot be below zero.",
                ProductPriceProblem.NegativeCompareAtPrice => "The compare-at price cannot be below zero.",
                ProductPriceProblem.NegativeSalePrice => "The sale price cannot be below zero.",
                ProductPriceProblem.SaleNotBelowPrice => $"The sale price has to be lower than the price ({price}).",
                _ => "The sale has to end after it starts.",
            }));
        }

        var (item, published, saveError) = await SaveProductAsync(services, caller, latest, ReadBool(arguments, "publish") == true, version =>
            version.Alter<ProductPart>(part =>
            {
                part.Price = price;
                part.CompareAtPrice = compareAtPrice;
                part.SalePrice = salePrice;
                part.SaleStartsUtc = saleStartsUtc;
                part.SaleEndsUtc = saleEndsUtc;
            }));

        if (item is null)
        {
            return saveError;
        }

        return Json(new
        {
            contentItemId = item.ContentItemId,
            title = item.DisplayText,
            currency,
            price,
            compareAtPrice,
            salePrice,
            saleStarts = await LocalTimeAsync(services, saleStartsUtc),
            saleEnds = await LocalTimeAsync(services, saleEndsUtc),
            published,
            message = published ? "The prices were changed and published." : "The prices were saved in a draft; they show in the shop once the product is published.",
        });
    }

    private static async Task<object> UpdateVariantAsync(
        AIFunctionArguments arguments,
        IServiceProvider services,
        ClaimsPrincipal caller,
        ContentItem latest,
        ProductPart product,
        string currency,
        string variantId,
        Dictionary<string, decimal?> amounts)
    {
        if (Has(arguments, "saleStarts") || Has(arguments, "saleEnds"))
        {
            return "Nothing was changed: a variant has no sale dates of its own. Set saleStarts and saleEnds on the product (without variantId); a variant's sale price applies during them.";
        }

        var variant = GetPart<ProductVariantsPart>(latest)?.Variants?.FirstOrDefault(entry => string.Equals(entry.VariantId, variantId, StringComparison.Ordinal));

        if (variant is null)
        {
            return $"Nothing was changed: the product has no variant with the id '{variantId}'. Use saveProductVariants or the product's editor to see its variants.";
        }

        var price = amounts.TryGetValue("price", out var p) ? p : variant.Price;
        var compareAtPrice = amounts.TryGetValue("compareAtPrice", out var c) ? c : variant.CompareAtPrice;
        var salePrice = amounts.TryGetValue("salePrice", out var s) ? s : variant.SalePrice;

        if (!ProductVariantRules.IsValidPrice(price) || !ProductVariantRules.IsValidPrice(compareAtPrice) || !ProductVariantRules.IsValidPrice(salePrice))
        {
            return "Nothing was changed: enter a price of zero or more.";
        }

        var (item, published, saveError) = await SaveProductAsync(services, caller, latest, ReadBool(arguments, "publish") == true, version =>
            version.Alter<ProductVariantsPart>(part =>
            {
                var target = part.Variants.First(entry => string.Equals(entry.VariantId, variantId, StringComparison.Ordinal));

                target.Price = price;
                target.CompareAtPrice = compareAtPrice;
                target.SalePrice = salePrice;
            }));

        if (item is null)
        {
            return saveError;
        }

        var regular = price ?? product.Price;

        return Json(new
        {
            contentItemId = item.ContentItemId,
            title = item.DisplayText,
            variantId,
            sku = variant.Sku,
            currency,
            price,
            compareAtPrice,
            salePrice,
            published,
            warning = salePrice is not null && regular is not null && salePrice >= regular
                ? "The sale price is not lower than the variant's price, so the shop does not show it as a sale."
                : null,
            message = published ? "The variant's prices were changed and published." : "The variant's prices were saved in a draft; they show in the shop once the product is published.",
        });
    }

    // A grouped product with products chosen sells them, not itself, so it needs no price of its own.
    private static bool HasGroupedProducts(ContentItem item)
        => GetPart<ProductGroupPart>(item)?.ProductIds?.Exists(id => !string.IsNullOrWhiteSpace(id)) == true;
}
