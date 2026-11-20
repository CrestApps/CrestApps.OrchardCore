using System.Data;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

/// <summary>
/// Imports a batch of rows into a target that is not a content type (an <see cref="IContentTransferRecordImporter"/>)
/// and records each row's outcome on the import's progress, as the content item pipeline does: a failed row with its
/// reason and kind, every other row as imported.
/// </summary>
internal static class RecordImportBatch
{
    /// <summary>
    /// Turns the rows of a batch, as the import reads them, into rows read by column name.
    /// </summary>
    /// <param name="rows">The rows by their number in the file.</param>
    /// <param name="columns">The file's columns.</param>
    /// <returns>The rows in file order.</returns>
    internal static List<ContentTransferImportRow> ToRows(IEnumerable<KeyValuePair<int, DataRow>> rows, DataColumnCollection columns)
        => rows
            .OrderBy(row => row.Key)
            .Select(row => new ContentTransferImportRow(
                row.Key,
                columns.Cast<DataColumn>().Select(column => new KeyValuePair<string, string>(
                    column.ColumnName,
                    row.Value[column] is DBNull or null ? null : row.Value[column].ToString()))))
            .ToList();

    /// <summary>
    /// Imports the rows and records their outcomes. When the importer throws, every row of the batch it did not fail
    /// itself fails with the message, and the import goes on with the next batch.
    /// </summary>
    internal static async Task ImportAsync(
        IContentTransferRecordImporter importer,
        ContentTransferEntry entry,
        IReadOnlyList<ContentTransferImportRow> rows,
        ImportFileProcessStatsPart progressPart,
        IStringLocalizer localizer,
        ImportRunTelemetry runTelemetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(importer);

        if (rows.Count == 0)
        {
            return;
        }

        Exception failure = null;

        try
        {
            await importer.ImportAsync(new ContentTransferImportBatch(entry, rows), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            failure = ex;
        }

        foreach (var row in rows)
        {
            if (row.Failed)
            {
                ImportFilesBackgroundTask.AddRowError(progressPart, row.RowIndex, row.Error, row.ErrorCategory ?? localizer["Could not be imported"]);
                runTelemetry.AddError(ContentTransferDiagnostics.ReasonValidation);

                continue;
            }

            if (failure is not null)
            {
                ImportFilesBackgroundTask.AddRowError(progressPart, row.RowIndex, failure.Message, localizer["Could not be imported"]);
                runTelemetry.AddRowException(row.RowIndex, failure);

                continue;
            }

            progressPart.ImportedCount++;
            runTelemetry.AddRow(ContentTransferDiagnostics.ResultImported);
            ImportFilesBackgroundTask.RemoveRowError(progressPart, row.RowIndex);
        }
    }
}
