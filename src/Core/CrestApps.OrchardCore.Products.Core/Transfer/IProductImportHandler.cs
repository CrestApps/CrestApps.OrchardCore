using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Transfer;

/// <summary>
/// Told about the products an import made or updated, once they're saved, such as to count their stock.
/// </summary>
public interface IProductImportHandler
{
    Task ImportedAsync(IReadOnlyList<ProductImportEntry> entries, CancellationToken cancellationToken = default);
}

/// <summary>
/// A product an import saved, and what the file said about it; each of its variants carries the variant it became.
/// </summary>
public sealed record ProductImportEntry(ContentItem ContentItem, ImportedProduct Product);
