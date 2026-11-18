using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;

/// <summary>
/// Reads the columns the QTI reader adds: a row whose item was not imported fails with the reason, so no broken question
/// is saved, and the source of an imported question names the uploaded file.
/// </summary>
internal sealed class QtiQuestionImportHandler : ContentImportHandlerBase, IContentImportHandler
{
    private const string SourceFileColumn = "QuestionPart_SourceFile";

    private readonly IEnumerable<IContentTransferFileFormatProvider> _formats;

    internal readonly IStringLocalizer S;

    public QtiQuestionImportHandler(IEnumerable<IContentTransferFileFormatProvider> formats, IStringLocalizer<QtiQuestionImportHandler> stringLocalizer)
    {
        _formats = formats;
        S = stringLocalizer;
    }

    /// <summary>
    /// Names the source of an imported question: an item file is its own source; an item of a package is named with the
    /// package.
    /// </summary>
    /// <param name="uploaded">The uploaded file name.</param>
    /// <param name="path">The item's path in the package, as the QTI reader wrote it.</param>
    /// <returns>The source file the question keeps.</returns>
    internal static string SourceFileOf(string uploaded, string path)
        => string.IsNullOrWhiteSpace(uploaded)
            ? path
            : string.IsNullOrWhiteSpace(path) || uploaded.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? uploaded : $"{uploaded}/{path}";

    /// <inheritdoc />
    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(ImportContentContext context)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>([]);

    /// <inheritdoc />
    public Task ImportAsync(ContentImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only a row the QTI reader wrote has the error column; the same column in another file is ignored.
        if (context.Columns is null || !context.Columns.Contains(QtiColumns.Error) || !QtiMediaImportHandler.ReadByQti(_formats, context.Entry))
        {
            return Task.CompletedTask;
        }

        var error = context.Row[QtiColumns.Error]?.ToString();

        if (!string.IsNullOrWhiteSpace(error))
        {
            context.Fail(error, S["Not imported from QTI"]);

            return Task.CompletedTask;
        }

        var uploaded = context.Entry?.UploadedFileName;

        if (!string.IsNullOrWhiteSpace(uploaded) && context.Columns.Contains(SourceFileColumn))
        {
            context.Row[SourceFileColumn] = SourceFileOf(uploaded, context.Row[SourceFileColumn]?.ToString());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ExportAsync(ContentExportContext context)
        => Task.CompletedTask;
}
