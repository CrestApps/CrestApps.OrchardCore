using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Imports;

/// <summary>
/// Reads bank questions from the first sheet of an Excel workbook with the columns
/// <see cref="SpreadsheetQuestionReader"/> describes.
/// </summary>
public sealed class ExcelQuestionImportFormat : ISurveyImportFormat
{
    /// <summary>
    /// The technical name of the format.
    /// </summary>
    public const string FormatName = "Excel";

    private readonly SpreadsheetQuestionReader _reader;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelQuestionImportFormat"/> class.
    /// </summary>
    /// <param name="reader">The spreadsheet reader.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public ExcelQuestionImportFormat(
        SpreadsheetQuestionReader reader,
        IStringLocalizer<ExcelQuestionImportFormat> stringLocalizer)
    {
        _reader = reader;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => FormatName;

    /// <inheritdoc />
    public string DisplayName => S["Excel workbook"];

    /// <inheritdoc />
    public IReadOnlyCollection<string> Extensions { get; } = [".xlsx"];

    /// <inheritdoc />
    public async Task<SurveyImportResult> ReadAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // OpenXml needs a seekable stream; an upload stream may not be one.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        if (!ExcelSheetReader.TryRead(buffer, SpreadsheetQuestionReader.MaxRows + 1, out var headers, out var rows))
        {
            var result = new SurveyImportResult();
            result.Errors.Add(S["The file is not an Excel workbook."]);

            return result;
        }

        return _reader.Read(headers, rows);
    }
}
