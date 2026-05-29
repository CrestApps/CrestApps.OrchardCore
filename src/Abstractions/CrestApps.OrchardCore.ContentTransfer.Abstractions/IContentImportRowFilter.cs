#nullable enable

using System.Data;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Filters rows during content import. Implementations can reject rows before
/// they are processed by the import pipeline (e.g., duplicate detection, DNC list checks).
/// </summary>
public interface IContentImportRowFilter
{
    /// <summary>
    /// Called once before import processing begins to allow the filter to initialize state
    /// for the active import.
    /// </summary>
    /// <param name="context">The context containing the entry metadata and content type information.</param>
    /// <returns>
    /// <see langword="true"/> when the filter should participate in row filtering for the
    /// current import; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> InitializeAsync(ContentImportRowFilterInitContext context);

    /// <summary>
    /// Determines whether a row should be skipped during import.
    /// </summary>
    /// <param name="context">The context containing the row data, entry metadata, and content type information.</param>
    /// <returns>
    /// <c>true</c> if the row should be skipped; <c>false</c> if it should be processed normally.
    /// </returns>
    Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context);
}
