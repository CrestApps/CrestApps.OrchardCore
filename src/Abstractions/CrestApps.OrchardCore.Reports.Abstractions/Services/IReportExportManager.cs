namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Lists the registered report export formats from their descriptors, and resolves a format to export a report.
/// </summary>
public interface IReportExportManager
{
    /// <summary>
    /// Lists the registered export formats, without creating them.
    /// </summary>
    /// <returns>The descriptors of the formats.</returns>
    IReadOnlyList<ReportExportFormatDescriptor> GetFormats();

    /// <summary>
    /// Finds the descriptor of an export format by its technical name, ignoring case.
    /// </summary>
    /// <param name="name">The technical name of the format.</param>
    /// <returns>The descriptor, or <see langword="null"/> when no format has this name.</returns>
    ReportExportFormatDescriptor GetDescriptor(string name);

    /// <summary>
    /// Resolves an export format by its technical name, ignoring case.
    /// </summary>
    /// <param name="name">The technical name of the format.</param>
    /// <returns>The format, or <see langword="null"/> when no format has this name.</returns>
    IReportExportFormat GetFormat(string name);
}
