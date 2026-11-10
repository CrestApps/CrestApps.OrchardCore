using System.Collections.Concurrent;
using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Services;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telemetry.ContentTransfer;

/// <summary>
/// Imports, exports and the Content Transfer services emit the <c>crestapps.contenttransfer.*</c> instruments and
/// spans the observability docs list, with bounded tags only. The names are a public contract that dashboards and
/// alerts depend on.
/// </summary>
public sealed class ContentTransferTelemetryTests
{
    private static readonly ImportRunLimits _limits = new(
        RunTimeout: TimeSpan.FromMinutes(5),
        RowTimeout: TimeSpan.FromMilliseconds(200),
        SlowRowThreshold: TimeSpan.FromMinutes(1),
        CheckInterval: TimeSpan.FromMilliseconds(20));

    [Fact]
    public async Task Watchdog_AbandoningARunStuckOnARow_CountsTheAbandonByReason()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var neverEnds = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var run = await ImportRunSupervisor.RunAsync(
            (monitor, _) =>
            {
                monitor.BeginRow(7);

                return neverEnds.Task;
            },
            _limits,
            NullLogger.Instance,
            "telemetry-entry",
            TestContext.Current.CancellationToken);

        neverEnds.SetResult(false);

        // Assert
        Assert.True(run.Abandoned);
        Assert.Equal(ContentTransferDiagnostics.ReasonRowTimeout, run.AbandonReason);
        Assert.NotEmpty(capture.Find("crestapps.contenttransfer.import.abandoned", CrestAppsOrchardCoreTelemetry.ReasonTag, ContentTransferDiagnostics.ReasonRowTimeout));
        Assert.Contains(
            capture.Find("crestapps.contenttransfer.import.stalled_row.elapsed", CrestAppsOrchardCoreTelemetry.ReasonTag, ContentTransferDiagnostics.ReasonRowTimeout),
            measurement => measurement.Value >= _limits.RowTimeout.TotalSeconds);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task FilterPendingRowsAsync_TimesTheFilters_AndCountsSkippedRowsOnlyOnceTheirProgressIsSaved()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var reasonCode = "t" + Guid.NewGuid().ToString("n")[..20];
        var dataTable = new DataTable();
        dataTable.Columns.Add("Phone");

        var entry = new ContentTransferEntry();
        var progressPart = new ImportFileProcessStatsPart { Errors = [], ErrorMessages = [] };
        var pendingRows = Enumerable.Range(1, 3)
            .Select(rowIndex => new ContentImportRowFilterContext
            {
                Row = dataTable.NewRow(),
                Columns = dataTable.Columns,
                Entry = entry,
                RowIndex = rowIndex,
            })
            .ToList();

        var runTelemetry = new ImportRunTelemetry(NullLogger.Instance, "telemetry-entry", firstRowOfRun: 0);

        // Act
        await ImportFilesBackgroundTask.FilterPendingRowsAsync(
            pendingRows,
            [new SkipRowFilter(skippedRowIndex: 2, reasonCode)],
            contentTypeDefinition: null,
            entry,
            progressPart,
            indexOfKeyColumn: -1,
            new Dictionary<int, DataRow>(),
            new Dictionary<string, KeyValuePair<int, DataRow>>(StringComparer.OrdinalIgnoreCase),
            monitor: null,
            TestContext.Current.CancellationToken,
            runTelemetry);

        var skippedBeforeSave = capture.Find("crestapps.contenttransfer.import.rows.skipped", CrestAppsOrchardCoreTelemetry.ReasonTag, reasonCode);

        runTelemetry.Commit();

        // Assert
        Assert.Empty(skippedBeforeSave);
        Assert.Equal(1, Assert.Single(capture.Find("crestapps.contenttransfer.import.rows.skipped", CrestAppsOrchardCoreTelemetry.ReasonTag, reasonCode)).Value);
        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.contenttransfer.import.filter");
        Assert.NotEmpty(capture.Find("crestapps.contenttransfer.import.filter.duration", ContentTransferDiagnostics.PhaseTag, ContentTransferDiagnostics.PhasePrepare));
        Assert.NotEmpty(capture.Find("crestapps.contenttransfer.import.filter.duration", ContentTransferDiagnostics.PhaseTag, ContentTransferDiagnostics.PhaseEvaluate));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void ImportRunTelemetry_CountsRowsByResult_AndLogsTheTypeOfARowException_NeverItsMessage()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var logger = new CapturingLogger();
        var entryId = "entry-" + Guid.NewGuid().ToString("n");
        var runTelemetry = new ImportRunTelemetry(logger, entryId, firstRowOfRun: 0);

        // Act
        runTelemetry.AddRow(ContentTransferDiagnostics.ResultImported);
        runTelemetry.AddRow(ContentTransferDiagnostics.ResultImported);
        runTelemetry.AddError(ContentTransferDiagnostics.ReasonValidation);
        runTelemetry.AddRowException(4, new InvalidOperationException("The cell value Jane Doe could not be read."));
        runTelemetry.Commit();

        // Assert
        Assert.Equal(0, runTelemetry.PendingRows);
        Assert.Contains(
            capture.Find("crestapps.contenttransfer.import.rows", CrestAppsOrchardCoreTelemetry.ResultTag, ContentTransferDiagnostics.ResultError),
            measurement => measurement.Value >= 2);

        Assert.NotEmpty(capture.Find("crestapps.contenttransfer.import.row.errors", CrestAppsOrchardCoreTelemetry.ReasonTag, ContentTransferDiagnostics.ReasonHandlerException));

        var message = Assert.Single(logger.Messages, message => message.Contains(entryId, StringComparison.Ordinal));
        Assert.Contains(typeof(InvalidOperationException).FullName, message, StringComparison.Ordinal);
        Assert.DoesNotContain("Jane Doe", message, StringComparison.Ordinal);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void ExportPage_CountsThePageAndItsRows_ByMode()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        ExportFilesBackgroundTask.RecordPage(ContentTransferDiagnostics.ModeInline, written: 3, excluded: 1);

        // Assert
        Assert.NotEmpty(capture.Find("crestapps.contenttransfer.export.pages", ContentTransferDiagnostics.ModeTag, ContentTransferDiagnostics.ModeInline));

        var rows = capture.Find("crestapps.contenttransfer.export.rows", ContentTransferDiagnostics.ModeTag, ContentTransferDiagnostics.ModeInline);
        Assert.Contains(rows, measurement => Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], ContentTransferDiagnostics.ResultWritten));
        Assert.Contains(rows, measurement => Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], ContentTransferDiagnostics.ResultExcluded));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Theory]
    [InlineData(".csv", "csv")]
    [InlineData("XLSX", "xlsx")]
    [InlineData("upload-2026.xlsx", "xlsx")]
    [InlineData("contacts.exe", "other")]
    [InlineData(null, "unknown")]
    public void GetFormat_ReturnsABoundedFormat(string value, string expected)
    {
        Assert.Equal(expected, ContentTransferDiagnostics.GetFormat(value));
    }

    [Theory]
    [InlineData("dnc_match", "dnc_match")]
    [InlineData("duplicate", "duplicate")]
    [InlineData("On a national registry", "other")]
    [InlineData("555-0100", "other")]
    [InlineData("", "other")]
    [InlineData(null, "other")]
    public void NormalizeCode_KeepsOnlyShortSnakeCaseCodes(string code, string expected)
    {
        Assert.Equal(expected, ContentTransferDiagnostics.NormalizeCode(code));
    }

    private sealed class SkipRowFilter(int skippedRowIndex, string reasonCode) : IContentImportRowFilter
    {
        public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
            => Task.FromResult(true);

        public Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
            => Task.CompletedTask;

        public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
        {
            if (context.RowIndex != skippedRowIndex)
            {
                return Task.FromResult(false);
            }

            context.SkipReason = "Skipped by the test filter.";
            context.SkipCategory = "Test reason";
            context.SkipReasonCode = reasonCode;

            return Task.FromResult(true);
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _messages.Enqueue(formatter(state, exception));
    }
}
