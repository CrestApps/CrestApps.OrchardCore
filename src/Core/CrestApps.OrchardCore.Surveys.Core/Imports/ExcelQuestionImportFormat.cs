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

    /// <summary>
    /// The largest workbook read, in bytes.
    /// </summary>
    public const long MaxBytes = 10 * 1024 * 1024;

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

        // OpenXml needs a seekable stream; an upload stream may not be one. The copy stops at the size limit.
        using var buffer = new MemoryStream();

        if (!await CopyAtMostAsync(content, buffer, MaxBytes, cancellationToken))
        {
            var refused = new SurveyImportResult();
            refused.Errors.Add(S["The file is larger than {0} MB.", MaxBytes / 1024 / 1024]);

            return refused;
        }

        buffer.Position = 0;

        if (!ExcelSheetReader.TryRead(buffer, SpreadsheetQuestionReader.MaxRows + 1, out var headers, out var rows, out var tooLarge))
        {
            var result = new SurveyImportResult();
            result.Errors.Add(tooLarge ? S["The workbook has more cells than can be imported."] : S["The file is not an Excel workbook."]);

            return result;
        }

        return _reader.Read(headers, rows);
    }

    private static async Task<bool> CopyAtMostAsync(Stream source, MemoryStream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;

            if (total > maxBytes)
            {
                return false;
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return true;
    }
}
