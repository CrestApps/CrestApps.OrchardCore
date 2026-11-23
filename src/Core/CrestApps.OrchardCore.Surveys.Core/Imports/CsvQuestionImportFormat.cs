using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tabular;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Imports;

/// <summary>
/// Reads bank questions from a CSV file with the columns <see cref="SpreadsheetQuestionReader"/> describes.
/// </summary>
public sealed class CsvQuestionImportFormat : ISurveyImportFormat
{
    /// <summary>
    /// The technical name of the format.
    /// </summary>
    public const string FormatName = "Csv";

    private readonly SpreadsheetQuestionReader _reader;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvQuestionImportFormat"/> class.
    /// </summary>
    /// <param name="reader">The spreadsheet reader.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CsvQuestionImportFormat(
        SpreadsheetQuestionReader reader,
        IStringLocalizer<CsvQuestionImportFormat> stringLocalizer)
    {
        _reader = reader;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => FormatName;

    /// <inheritdoc />
    public string DisplayName => S["CSV file"];

    /// <inheritdoc />
    public IReadOnlyCollection<string> Extensions { get; } = [".csv"];

    /// <inheritdoc />
    public async Task<SurveyImportResult> ReadAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The copy stops at the size limit, so an upload of any size is never read whole.
        using var buffer = await QuestionImportUploads.ReadAtMostAsync(content, QuestionImportUploads.MaxSpreadsheetBytes, cancellationToken);

        if (buffer is null)
        {
            var refused = new SurveyImportResult();
            refused.Errors.Add(S["The file is larger than {0} MB.", QuestionImportUploads.MaxSpreadsheetBytes / 1024 / 1024]);

            return refused;
        }

        using var reader = new StreamReader(buffer, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var (headers, rows) = CsvReader.Parse(text);

        return _reader.Read(headers, rows);
    }
}
