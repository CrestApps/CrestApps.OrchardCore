using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.ContentTransfer;

/// <summary>
/// Imports and exports a product's variants: its options and their values in one column, such as
/// <c>Size: S, M; Color: Red, Blue</c>, and in another each variant that differs from the product, as JSON, such as
/// <c>[{"options":{"Size":"S","Color":"Red"},"sku":"TEE-S-R","price":20}]</c>. Only the options the product type
/// offers are kept; a combination the file doesn't list is switched off when the file lists variants.
/// </summary>
public sealed class ProductVariantsPartContentImportHandler : ContentImportHandlerBase, IContentPartImportHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal readonly IStringLocalizer S;

    public ProductVariantsPartContentImportHandler(IStringLocalizer<ProductVariantsPartContentImportHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
    {
        var prefix = (context?.ContentTypePartDefinition?.Name ?? nameof(ProductVariantsPart)) + "_";

        return
        [
            new() { Name = prefix + "Options", Description = S["The options and their values, such as Size: S, M; Color: Red, Blue. Only the options the product type offers are kept."] },
            new() { Name = prefix + "Variants", Description = S["Each variant's options, SKU, prices and whether it's sold, as a JSON list. A combination the list leaves out isn't sold."] },
        ];
    }

    public Task ImportAsync(ContentPartImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition?.Name ?? nameof(ProductVariantsPart);
        var columns = GetColumns(context).ToArray();
        string options = null, variants = null;

        foreach (DataColumn column in context.Columns)
        {
            var value = context.Row[column]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (Is(column.ColumnName, columns[0]))
            {
                options = value;
            }
            else if (Is(column.ColumnName, columns[1]))
            {
                variants = value;
            }
        }

        if (options is null)
        {
            return Task.CompletedTask;
        }

        var offered = OfferedOptions(context.ContentTypePartDefinition);
        var part = context.ContentItem.Get<ProductVariantsPart>(name) ?? new ProductVariantsPart();

        // The type's spelling of each option it offers, in the type's order.
        var parsed = ParseOptions(options);
        part.Options = [.. offered
            .Select(optionName => parsed.FirstOrDefault(option => string.Equals(option.Name, optionName, StringComparison.OrdinalIgnoreCase)) is { } option
                ? new ProductOption { Name = optionName, Values = option.Values }
                : null)
            .Where(option => option is not null && option.Values.Count > 0)];
        part.Variants = ProductVariants.Combine(part.Options, part.Variants, UniqueId.GenerateId);

        if (variants is not null && TryReadVariants(variants, out var rows))
        {
            foreach (var variant in part.Variants)
            {
                var row = rows.FirstOrDefault(entry => Matches(entry.Options, variant.Options));

                variant.Enabled = row is not null && row.Enabled != false;

                if (row is not null)
                {
                    variant.Sku = string.IsNullOrWhiteSpace(row.Sku) ? variant.Sku : row.Sku.Trim();
                    variant.Price = row.Price ?? variant.Price;
                    variant.CompareAtPrice = row.CompareAtPrice ?? variant.CompareAtPrice;
                    variant.SalePrice = row.SalePrice ?? variant.SalePrice;
                }
            }
        }

        context.ContentItem.Apply(name, part);

        return Task.CompletedTask;
    }

    public Task ExportAsync(ContentPartExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition?.Name ?? nameof(ProductVariantsPart);

        if (context.ContentItem.Get<ProductVariantsPart>(name) is not { HasVariants: true } part)
        {
            return Task.CompletedTask;
        }

        context.Row[name + "_Options"] = WriteOptions(part.Options);
        context.Row[name + "_Variants"] = JsonSerializer.Serialize(part.Variants.Select(variant => new VariantRow
        {
            Options = new Dictionary<string, string>(variant.Options, StringComparer.OrdinalIgnoreCase),
            Sku = variant.Sku,
            Price = variant.Price,
            CompareAtPrice = variant.CompareAtPrice,
            SalePrice = variant.SalePrice,
            Enabled = variant.Enabled ? null : false,
        }), JsonOptions);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads "Size: S, M; Color: Red, Blue".
    /// </summary>
    public static List<ProductOption> ParseOptions(string text)
        => [.. (text ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split(':', 2))
            .Where(pair => pair.Length == 2 && !string.IsNullOrWhiteSpace(pair[0]))
            .Select(pair => new ProductOption
            {
                Name = pair[0].Trim(),
                Values = [.. pair[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)],
            })];

    public static string WriteOptions(IEnumerable<ProductOption> options)
        => string.Join("; ", (options ?? []).Where(option => option.Values?.Count > 0).Select(option => $"{option.Name}: {string.Join(", ", option.Values)}"));

    internal static string[] OfferedOptions(ContentTypePartDefinition definition)
        => [.. definition?.GetSettings<ProductVariantsPartSettings>()?.Options?.Select(option => option.Name).Where(optionName => !string.IsNullOrWhiteSpace(optionName)) ?? []];

    private static bool TryReadVariants(string json, out List<VariantRow> rows)
    {
        try
        {
            rows = JsonSerializer.Deserialize<List<VariantRow>>(json, JsonOptions) ?? [];

            return true;
        }
        catch (JsonException)
        {
            rows = null;

            return false;
        }
    }

    private static bool Matches(Dictionary<string, string> wanted, Dictionary<string, string> variant)
        => wanted is { Count: > 0 } &&
            wanted.Count == variant.Count &&
            wanted.All(pair => variant.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A variant as the Variants column writes it.
    /// </summary>
    public sealed class VariantRow
    {
        public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string Sku { get; set; }

        public decimal? Price { get; set; }

        public decimal? CompareAtPrice { get; set; }

        public decimal? SalePrice { get; set; }

        public bool? Enabled { get; set; }
    }
}
