using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Defines an export format for a report document, such as CSV, Excel, or PDF. What describes the format, such as its
/// name and its extension, is static, so formats are listed without being created; a module registers one with
/// <c>services.AddReportExportFormat&lt;TFormat&gt;()</c>.
/// </summary>
public interface IReportExportFormat
{
    /// <summary>
    /// Gets the stable technical name of the format, such as <c>csv</c>.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Gets the label shown to users.
    /// </summary>
    static abstract LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the file extension of the format, with its leading dot, such as <c>.csv</c>.
    /// </summary>
    static abstract string Extension { get; }

    /// <summary>
    /// Gets the media type of the format, such as <c>text/csv</c>.
    /// </summary>
    static abstract string ContentType { get; }

    /// <summary>
    /// Serializes a report document in the format.
    /// </summary>
    /// <param name="document">The report document.</param>
    /// <returns>The content of the file.</returns>
    byte[] Serialize(ReportDocument document);
}
