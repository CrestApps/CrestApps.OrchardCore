using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Reads a file laid out by another system as a content type's import columns, such as another store's product
/// export, where each product spans a row per variant, into one row per content item with the columns the part and
/// field handlers read. The first transformer that recognizes a file reads it; a file none recognizes is read as is.
/// </summary>
public interface IContentTransferFileReaderTransformer
{
    /// <summary>
    /// Returns a reader of the file as the content type's columns, or <see langword="null"/> when the file isn't one
    /// this transformer reads.
    /// </summary>
    /// <param name="reader">The file as its format reads it, positioned at its start.</param>
    /// <param name="contentType">The content type the file is imported as.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IContentTransferFileReader> TransformAsync(IContentTransferFileReader reader, ContentTypeDefinition contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns what the import page tells the merchant this transformer also reads for the content type, such as
    /// "Shopify's and WooCommerce's product exports", or <see langword="null"/> when it reads nothing for it.
    /// </summary>
    string Describe(ContentTypeDefinition contentType);
}
