using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Defines an export format for a report document. A module contributes a new export format (for
/// example CSV, Excel, or PDF) by registering an implementation of this interface.
/// </summary>
public interface IReportExportFormat
{
    /// <summary>
    /// Gets the stable technical name of the format used to resolve it (for example <c>csv</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the localized, human-readable name of the format.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the MIME content type produced by the format.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the file extension (without a leading dot) produced by the format.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Serializes the report document into the format's byte representation.
    /// </summary>
    /// <param name="document">The report document to serialize.</param>
    /// <returns>The serialized content.</returns>
    byte[] Serialize(ReportDocument document);
}
