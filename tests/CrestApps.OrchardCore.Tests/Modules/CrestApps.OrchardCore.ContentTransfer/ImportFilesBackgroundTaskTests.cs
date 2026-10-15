using System.Data;
using CrestApps.OrchardCore.BackgroundWork;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ImportFilesBackgroundTaskTests
{
    [Fact]
    public void GetImportContentItemId_ShouldReturnContentItemId()
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(ContentItem.ContentItemId));
        dataTable.Columns.Add(nameof(ContentItem.ContentItemVersionId));
        var row = dataTable.NewRow();
        row[nameof(ContentItem.ContentItemId)] = "4eb8n2j6ffk0q6dg9jevfavb5y";
        row[nameof(ContentItem.ContentItemVersionId)] = "version-id-from-export";
        dataTable.Rows.Add(row);

        var contentItemId = ImportFilesBackgroundTask.GetImportContentItemId(
            row,
            dataTable.Columns.IndexOf(nameof(ContentItem.ContentItemId)));

        Assert.Equal("4eb8n2j6ffk0q6dg9jevfavb5y", contentItemId);
    }

    [Fact]
    public void GetImportContentItemId_ShouldIgnoreContentItemVersionId()
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(ContentItem.ContentItemVersionId));
        var row = dataTable.NewRow();
        row[nameof(ContentItem.ContentItemVersionId)] = "version-id-from-export";
        dataTable.Rows.Add(row);

        var contentItemId = ImportFilesBackgroundTask.GetImportContentItemId(
            row,
            indexOfKeyColumn: -1);

        Assert.Null(contentItemId);
    }

    [Fact]
    public void AddRowError_KeepsEveryFailedRow_ButCapsStoredMessages()
    {
        var progressPart = new ImportFileProcessStatsPart();
        var failedRows = ImportFileProcessStatsPart.MaxStoredErrorMessages + 250;

        for (var rowIndex = 1; rowIndex <= failedRows; rowIndex++)
        {
            ImportFilesBackgroundTask.AddRowError(progressPart, rowIndex, $"Row {rowIndex} failed.");
        }

        Assert.Equal(failedRows, progressPart.Errors.Count);
        Assert.Equal(ImportFileProcessStatsPart.MaxStoredErrorMessages, progressPart.ErrorMessages.Count);
        Assert.Equal("Row 1 failed.", progressPart.ErrorMessages[1]);
        Assert.False(progressPart.ErrorMessages.ContainsKey(failedRows));
    }

    [Fact]
    public void AddRowError_UpdatesAStoredMessage_AfterTheCapIsReached()
    {
        var progressPart = new ImportFileProcessStatsPart();

        for (var rowIndex = 1; rowIndex <= ImportFileProcessStatsPart.MaxStoredErrorMessages; rowIndex++)
        {
            ImportFilesBackgroundTask.AddRowError(progressPart, rowIndex, "First attempt.");
        }

        // A resumed run that fails a row again replaces its message rather than being refused by the cap.
        ImportFilesBackgroundTask.AddRowError(progressPart, 1, "Second attempt.");

        Assert.Equal("Second attempt.", progressPart.ErrorMessages[1]);
    }

    [Fact]
    public void RecordStalledRow_CountsRunsStuckOnTheSameRow()
    {
        var progressPart = new ImportFileProcessStatsPart();

        ImportFilesBackgroundTask.RecordStalledRow(progressPart, 104_967);
        ImportFilesBackgroundTask.RecordStalledRow(progressPart, 104_967);

        Assert.Equal(104_967, progressPart.StalledRow);
        Assert.Equal(2, progressPart.StalledRowAttempts);
    }

    [Fact]
    public void RecordStalledRow_StartsOverWhenAnotherRowStalls()
    {
        var progressPart = new ImportFileProcessStatsPart();

        ImportFilesBackgroundTask.RecordStalledRow(progressPart, 40);
        ImportFilesBackgroundTask.RecordStalledRow(progressPart, 41);

        // A run stuck on a different row was a passing outage, not the earlier row failing again.
        Assert.Equal(41, progressPart.StalledRow);
        Assert.Equal(1, progressPart.StalledRowAttempts);
    }

    [Fact]
    public void ShouldSkipStalledRow_SkipsOnlyTheRowThatStoppedEveryAllowedRun()
    {
        var progressPart = new ImportFileProcessStatsPart();

        ImportFilesBackgroundTask.RecordStalledRow(progressPart, 9);
        Assert.False(ImportFilesBackgroundTask.ShouldSkipStalledRow(progressPart, 9));

        for (var attempt = 1; attempt < ImportFilesBackgroundTask.MaxStalledRowAttempts; attempt++)
        {
            ImportFilesBackgroundTask.RecordStalledRow(progressPart, 9);
        }

        Assert.True(ImportFilesBackgroundTask.ShouldSkipStalledRow(progressPart, 9));
        Assert.False(ImportFilesBackgroundTask.ShouldSkipStalledRow(progressPart, 10));
    }

    [Fact]
    public void RunLimits_StopARunBeforeItsLockExpires_AndLeaveRoomForTheLastBatch()
    {
        var limits = ImportFilesBackgroundTask.RunLimits;

        // A run abandoned after its lock expired could overlap a run another instance started on the same file.
        Assert.True(limits.RunTimeout < TimeSpan.FromMinutes(30));

        // A healthy run stops reading at MaxRunDuration and may then wait for the shared gate before its last batch;
        // the watchdog must not give up on it while it does.
        Assert.True(ImportFilesBackgroundTask.MaxRunDuration + BackgroundWorkPacer.GateWaitLimit + TimeSpan.FromMinutes(5) <= limits.RunTimeout);
        Assert.True(limits.SlowRowThreshold < limits.RowTimeout);
        Assert.True(limits.RowTimeout < limits.RunTimeout);
    }
}
