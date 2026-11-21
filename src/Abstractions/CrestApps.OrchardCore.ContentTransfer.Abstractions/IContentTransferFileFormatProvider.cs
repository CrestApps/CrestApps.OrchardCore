using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Provides file format support for content transfer operations (import/export).
/// </summary>
public interface IContentTransferFileFormatProvider
{
    /// <summary>
    /// Gets the file extension including the dot (e.g., ".xlsx", ".csv").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Gets the MIME content type for this format.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Determines whether this provider can handle files with the given extension.
    /// </summary>
    bool CanHandle(string fileName);

    /// <summary>
    /// Creates a reader for importing data from the given stream.
    /// </summary>
    IContentTransferFileReader CreateReader(Stream stream);

    /// <summary>
    /// Creates a writer for exporting data to the given stream.
    /// </summary>
    /// <param name="stream">The output stream to write to.</param>
    /// <param name="sheetName">A display name for the data set (used as sheet name in Excel, ignored in CSV).</param>
    IContentTransferFileWriter CreateWriter(Stream stream, string sheetName);

    /// <summary>
    /// Gets whether the format can write files: exports, templates and the failed rows of an import. A format that
    /// only reads, such as a package another system exports, returns <see langword="false"/> and is offered for import
    /// only; its <see cref="CreateWriter"/> is never called.
    /// </summary>
    bool CanExport => true;

    /// <summary>
    /// Determines whether files of this format can be imported as the content type. The import page offers the format,
    /// accepts its files and reads them only for the content types it accepts, so two formats may share an extension
    /// when they import different content types.
    /// </summary>
    /// <param name="contentType">The content type the file is imported as.</param>
    bool CanImport(string contentType) => true;

    /// <summary>
    /// Returns what the import page tells the person about files of this format for the content type, such as "A QTI
    /// content package: each item becomes the kind of question that fits it", or <see langword="null"/> when the
    /// format needs no explanation.
    /// </summary>
    /// <param name="contentType">The content type the file is imported as.</param>
    string DescribeImport(ContentTypeDefinition contentType) => null;
}
