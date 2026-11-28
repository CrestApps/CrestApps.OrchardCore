using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Sets a product's option values and its variants' SKU, prices, image and switch, with the variant editor's rules
/// (<see cref="ProductVariantRules"/>): the options are the ones the content type defines; the variants are generated from
/// the values by <see cref="ProductVariants.Combine"/>, so a combination that stays keeps its variant id, and stock and
/// past orders keep pointing at it; prices are zero or more; no SKU is used by two variants. Stricter than the editor: an
/// option the type does not define is refused (the editor drops it), an image must be one of the product's own photos
/// (the editor offers only those), every amount must fit the currency's decimal places, and at most
/// <see cref="MaxVariants"/> combinations are made. It needs <c>EditContent</c> on the product, and <c>PublishContent</c>
/// to publish.
/// </summary>
internal sealed class SaveProductVariantsTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveProductVariants";

    /// <summary>
    /// The most variants the tool makes for one product.
    /// </summary>
    public const int MaxVariants = 250;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The product's content item id, from listProducts."
        },
        "options": {
          "type": "object",
          "description": "The values of each option, by option name, such as {\"Size\": [\"S\", \"M\", \"L\"]}. Only options the product's content type defines can be given; an option left out keeps its values, and an empty list removes it. Variants are made for every combination of the values.",
          "additionalProperties": { "type": "array", "items": { "type": "string" } }
        },
        "variants": {
          "type": "array",
          "description": "Changes to variants, after they are made from the options. Each names a variant by its id or by its option values.",
          "items": {
            "type": "object",
            "properties": {
              "variantId": { "type": "string", "description": "The variant's id." },
              "options": {
                "type": "object",
                "description": "The variant's option values, such as {\"Size\": \"M\", \"Color\": \"Blue\"}, to find it when its id is not known.",
                "additionalProperties": { "type": "string" }
              },
              "sku": { "type": ["string", "null"], "description": "The variant's SKU, unique among the product's variants. null clears it." },
              "price": { "type": ["number", "null"], "description": "The variant's own price. null makes it sell at the product's price." },
              "compareAtPrice": { "type": ["number", "null"], "description": "The price shown struck through. null clears it." },
              "salePrice": { "type": ["number", "null"], "description": "The variant's sale price, applied during the product's sale dates. null clears it." },
              "imagePath": { "type": ["string", "null"], "description": "One of the product's own photos, by its media path, shown when the variant is picked. null clears it." },
              "enabled": { "type": "boolean", "description": "false to stop selling the variant without removing it." }
            },
            "additionalProperties": false
          }
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

    private static readonly HashSet<string> _variantFields = new(StringComparer.Ordinal)
    {
        "variantId", "options", "sku", "price", "compareAtPrice", "salePrice", "imagePath", "enabled",
    };

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets a product's options (for example Size and Color) and its variants' SKU, price, compare-at price, sale price, image and enabled flag. Variants are generated from the options and existing variant ids are kept, so stock and past orders keep pointing at them; it refuses a SKU used by two variants and saves a draft unless told to publish.";

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

        var typePart = type.Parts.FirstOrDefault(part => part.PartDefinition?.Name == nameof(ProductVariantsPart));

        if (typePart is null)
        {
            return $"Nothing was changed: the content type '{type.Name}' has no Product Variants part. Add the part to the type first.";
        }

        var names = ProductVariantRules.GetOptionNames(typePart);

        if (names.Length == 0)
        {
            return $"Nothing was changed: the content type '{type.Name}' defines no variant options. Add them, such as Size or Color, to its Product Variants part settings first.";
        }

        // The change is worked out on a copy, so nothing is saved unless all of it is valid.
        var stored = GetPart<ProductVariantsPart>(latest) ?? new ProductVariantsPart();
        var part = new ProductVariantsPart
        {
            Options = [.. (stored.Options ?? []).Select(option => new ProductOption { Name = option.Name, Values = [.. option.Values ?? []] })],
            Variants = [.. (stored.Variants ?? []).Where(variant => variant is not null).Select(Copy)],
        };
        var values = names.ToDictionary(
            name => name,
            name => (IEnumerable<string>)(stored.Options?.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))?.Values ?? []),
            StringComparer.OrdinalIgnoreCase);

        if (ReadJson(arguments, "options") is { } options)
        {
            if (options.ValueKind != JsonValueKind.Object)
            {
                return "Nothing was changed: give options as an object of option names and lists of values.";
            }

            foreach (var option in options.EnumerateObject())
            {
                if (!values.ContainsKey(option.Name))
                {
                    return $"Nothing was changed: the content type '{type.Name}' has no option '{option.Name}'. Its options are: {string.Join(", ", names)}. Add an option to the type's Product Variants part settings first.";
                }

                if (option.Value.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null))
                {
                    return $"Nothing was changed: give the values of '{option.Name}' as a list, such as [\"S\", \"M\"].";
                }

                values[option.Name] = option.Value.ValueKind == JsonValueKind.Null
                    ? []
                    : option.Value.EnumerateArray().Select(ToText).Where(value => value is not null).ToArray();
            }
        }

        var built = ProductVariantRules.BuildOptions(names, name => values[name]);
        var combinations = built.Aggregate(1L, (count, option) => count * option.Values.Count);

        if (built.Count > 0 && combinations > MaxVariants)
        {
            return $"Nothing was changed: those values make {combinations} variants; at most {MaxVariants} are made at once.";
        }

        var before = (stored.Variants ?? []).Where(variant => variant.VariantId is not null).ToDictionary(variant => variant.VariantId, StringComparer.Ordinal);

        ProductVariantRules.Apply(part, built);

        var currency = GetCurrency(GetPart<ProductPart>(latest), type);
        var gallery = ProductGalleryReader.Find(type, latest)?.Paths ?? [];

        if (ReadJson(arguments, "variants") is { } changes)
        {
            if (changes.ValueKind != JsonValueKind.Array)
            {
                return "Nothing was changed: give variants as a list.";
            }

            var index = 0;

            foreach (var change in changes.EnumerateArray())
            {
                index++;

                if (ApplyChange(change, index, part, names, currency, gallery) is { } changeError)
                {
                    return changeError;
                }
            }
        }

        var duplicates = ProductVariantRules.FindDuplicateSkus(part.Variants);

        if (duplicates.Count > 0)
        {
            return $"Nothing was changed: the SKU '{duplicates[0]}' is used by more than one variant.";
        }

        var (item, published, saveError) = await SaveProductAsync(services, caller, latest, ReadBool(arguments, "publish") == true, version =>
            version.Alter<ProductVariantsPart>(target =>
            {
                target.Options = part.Options;
                target.Variants = part.Variants;
            }));

        if (item is null)
        {
            return saveError;
        }

        var kept = part.Variants.Select(variant => variant.VariantId).ToHashSet(StringComparer.Ordinal);

        return Json(new
        {
            contentItemId = item.ContentItemId,
            title = item.DisplayText,
            currency,
            options = part.Options.Select(option => new { name = option.Name, values = option.Values }).ToArray(),
            variants = part.Variants.Select(variant => new
            {
                variantId = variant.VariantId,
                title = variant.GetTitle(names),
                isNew = !before.ContainsKey(variant.VariantId),
                sku = variant.Sku,
                price = variant.Price,
                compareAtPrice = variant.CompareAtPrice,
                salePrice = variant.SalePrice,
                imagePath = variant.ImagePath,
                enabled = variant.Enabled,
            }).ToArray(),
            removed = before.Values.Where(variant => !kept.Contains(variant.VariantId)).Select(variant => new
            {
                variantId = variant.VariantId,
                title = variant.GetTitle(names),
                sku = variant.Sku,
            }).ToArray(),
            published,
            message = published
                ? "The variants were changed and published. A removed variant is no longer sold, and its stock is no longer counted."
                : "The variants were saved in a draft; they show in the shop once the product is published.",
        });
    }

    private static string ApplyChange(JsonElement change, int index, ProductVariantsPart part, string[] names, string currency, IReadOnlyList<string> gallery)
    {
        if (change.ValueKind != JsonValueKind.Object)
        {
            return $"Nothing was changed: variant change {index} is not an object.";
        }

        foreach (var property in change.EnumerateObject())
        {
            if (!_variantFields.Contains(property.Name))
            {
                return $"Nothing was changed: '{property.Name}' is not a field of a variant change.";
            }
        }

        var variant = FindVariant(change, part);

        if (variant is null)
        {
            return $"Nothing was changed: variant change {index} names no variant of the product. Name it by variantId or by a value of every option ({string.Join(", ", names)}), after the options are applied.";
        }

        var label = variant.GetTitle(names);

        foreach (var field in new[] { "price", "compareAtPrice", "salePrice" })
        {
            if (!change.TryGetProperty(field, out var element))
            {
                continue;
            }

            decimal? amount = null;

            if (element.ValueKind != JsonValueKind.Null)
            {
                if (!TryReadDecimal(element, out var number))
                {
                    return $"Nothing was changed: the {field} of {label} must be a number.";
                }

                amount = number;
            }

            if (!ProductVariantRules.IsValidPrice(amount))
            {
                return $"Nothing was changed: the {field} of {label}: enter a price of zero or more.";
            }

            if (CheckAmount($"the {field} of {label}", amount, currency) is { } amountError)
            {
                return amountError;
            }

            switch (field)
            {
                case "price":
                    variant.Price = amount;
                    break;
                case "compareAtPrice":
                    variant.CompareAtPrice = amount;
                    break;
                default:
                    variant.SalePrice = amount;
                    break;
            }
        }

        if (change.TryGetProperty("sku", out var sku))
        {
            variant.Sku = ToText(sku);
        }

        if (change.TryGetProperty("imagePath", out var image))
        {
            var path = ToText(image);

            if (path is not null && !gallery.Contains(path, StringComparer.Ordinal))
            {
                return gallery.Count == 0
                    ? $"Nothing was changed: the product has no photos to show for {label}. Add photos to the product first."
                    : $"Nothing was changed: '{path}' is not one of the product's photos. Its photos are: {string.Join(", ", gallery)}.";
            }

            variant.ImagePath = path;
        }

        if (change.TryGetProperty("enabled", out var enabled))
        {
            if (enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return $"Nothing was changed: enabled of {label} must be true or false.";
            }

            variant.Enabled = enabled.GetBoolean();
        }

        return null;
    }

    // A copy of a variant, every field included, with its option values found by name in any case.
    private static ProductVariant Copy(ProductVariant variant)
    {
        var copy = JsonSerializer.Deserialize<ProductVariant>(JsonSerializer.Serialize(variant));

        copy.Options = new Dictionary<string, string>(copy.Options ?? [], StringComparer.OrdinalIgnoreCase);

        return copy;
    }

    private static ProductVariant FindVariant(JsonElement change, ProductVariantsPart part)
    {
        if (change.TryGetProperty("variantId", out var id) && ToText(id) is { } variantId)
        {
            return part.Variants.FirstOrDefault(variant => string.Equals(variant.VariantId, variantId, StringComparison.Ordinal));
        }

        if (!change.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var wanted = options.EnumerateObject().ToDictionary(property => property.Name, property => ToText(property.Value), StringComparer.OrdinalIgnoreCase);
        var used = part.Options.Select(option => option.Name).ToArray();

        if (used.Length == 0 || used.Any(name => !wanted.ContainsKey(name)))
        {
            return null;
        }

        return part.Variants.FirstOrDefault(variant => used.All(name =>
            variant.Options.TryGetValue(name, out var value) && string.Equals(value, wanted[name], StringComparison.OrdinalIgnoreCase)));
    }
}
