using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Lets one import create content items of several related content types, such as a file of questions of different
/// kinds. When a provider names the types for the content type a file is imported as, each row may give its own type
/// in the <see cref="ContentTransferColumns.ContentType"/> column; a row with no type is imported as the content type
/// of the import. The import page keeps only the types the person may import, and a row naming any other type fails.
/// </summary>
public interface IContentImportRowTypeProvider
{
    /// <summary>
    /// Returns the content types the rows of an import of <paramref name="contentType"/> may be, itself included, or
    /// nothing when this provider has no group for it.
    /// </summary>
    /// <param name="contentType">The content type the file is imported as.</param>
    IEnumerable<string> GetRowContentTypes(ContentTypeDefinition contentType);
}
