using System.Data;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.ContentTransfer;

/// <summary>
/// Finds the products a file's rows name by SKU, their own or a variant's, so a row without a content item id updates
/// the product already in the store instead of adding another. A row whose SKU an earlier row of the file used is
/// skipped, with why. Each batch's SKUs are looked up in one query of the SKU index.
/// </summary>
public sealed class ProductImportRowFilter : IContentImportRowFilter
{
    private readonly ISession _session;

    private string _contentType;
    private string _skuColumn;
    private string _variantsColumn;
    private Dictionary<string, string> _existing = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _seen = new(StringComparer.OrdinalIgnoreCase);

    internal readonly IStringLocalizer S;

    public ProductImportRowFilter(ISession session, IStringLocalizer<ProductImportRowFilter> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
    {
        var type = context.ContentTypeDefinition;
        var productPart = type?.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart));

        if (productPart is null)
        {
            return Task.FromResult(false);
        }

        _contentType = type.Name;
        _skuColumn = productPart.Name + "_" + nameof(ProductPart.Sku);
        _variantsColumn = type.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductVariantsPart)) is { } variants
            ? variants.Name + "_Variants"
            : null;

        return Task.FromResult(true);
    }

    public async Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
    {
        var skus = context.Rows
            .Where(row => string.IsNullOrEmpty(ContentItemId(row.Row, row.Columns)))
            .SelectMany(row => Skus(row.Row, row.Columns))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (skus.Length == 0)
        {
            return;
        }

        var contentType = _contentType;

        foreach (var chunk in skus.Chunk(500))
        {
            var rows = await _session.QueryIndex<ProductSkuIndex>(index => index.ContentType == contentType && index.Latest && index.Sku.IsIn(chunk))
                .ListAsync(context.CancellationToken);

            foreach (var row in rows)
            {
                _existing.TryAdd(row.Sku, row.ContentItemId);
            }
        }
    }

    public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
    {
        var skus = Skus(context.Row, context.Columns).ToArray();

        foreach (var sku in skus)
        {
            if (_seen.TryGetValue(sku, out var earlier) && earlier != context.RowIndex)
            {
                context.SkipReason = S["The SKU '{0}' is on row {1} of the file already.", sku, earlier];

                return Task.FromResult(true);
            }
        }

        foreach (var sku in skus)
        {
            _seen[sku] = context.RowIndex;
        }

        // A row that names its content item updates it; otherwise the product with its SKU, if there is one.
        if (string.IsNullOrEmpty(ContentItemId(context.Row, context.Columns)) &&
            skus.Select(sku => _existing.GetValueOrDefault(sku)).FirstOrDefault(id => id is not null) is { } match)
        {
            if (!context.Columns.Contains(nameof(ContentItem.ContentItemId)))
            {
                context.SkipReason = S["A product with the SKU '{0}' is in the store already. To update it, add the ContentItemId column, as an export has.", skus[0]];

                return Task.FromResult(true);
            }

            context.Row[nameof(ContentItem.ContentItemId)] = match;
        }

        return Task.FromResult(false);
    }

    private static string ContentItemId(DataRow row, DataColumnCollection columns)
        => columns.Contains(nameof(ContentItem.ContentItemId)) ? row[nameof(ContentItem.ContentItemId)]?.ToString()?.Trim() : null;

    // The row's SKU and its variants' SKUs.
    private IEnumerable<string> Skus(DataRow row, DataColumnCollection columns)
    {
        if (columns.Contains(_skuColumn) && row[_skuColumn]?.ToString()?.Trim() is { Length: > 0 } sku)
        {
            yield return sku;
        }

        if (_variantsColumn is null || !columns.Contains(_variantsColumn) || row[_variantsColumn]?.ToString() is not { Length: > 0 } json)
        {
            yield break;
        }

        List<ProductVariantsPartContentImportHandler.VariantRow> variants;

        try
        {
            variants = JsonSerializer.Deserialize<List<ProductVariantsPartContentImportHandler.VariantRow>>(json, ProductVariantsPartContentImportHandler.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            yield break;
        }

        foreach (var variant in variants.Where(variant => !string.IsNullOrWhiteSpace(variant.Sku)))
        {
            yield return variant.Sku.Trim();
        }
    }
}
