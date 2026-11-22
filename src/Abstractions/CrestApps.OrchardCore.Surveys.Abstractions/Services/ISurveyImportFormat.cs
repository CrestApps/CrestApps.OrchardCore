using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Reads questions from a file into a question bank, such as a spreadsheet or QTI. Register an implementation with
/// <c>services.AddSurveyImportFormat&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyImportFormat
{
    /// <summary>
    /// Gets the unique name of the format.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the file extensions the format reads, such as .csv.
    /// </summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>
    /// Reads questions from a file.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="content">The file content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The questions read and the problems found.</returns>
    Task<SurveyImportResult> ReadAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads questions from a file for a dry run, without keeping anything. A format that stores something while it
    /// reads, such as the images of a package, overrides it to check those files without storing them.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="content">The file content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The questions read and the problems found.</returns>
    Task<SurveyImportResult> CheckAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
        => ReadAsync(fileName, content, cancellationToken);
}
