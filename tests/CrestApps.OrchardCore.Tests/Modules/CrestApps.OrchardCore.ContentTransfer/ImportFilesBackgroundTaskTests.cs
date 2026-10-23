using System.Data;
using CrestApps.OrchardCore.BackgroundWork;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ImportFilesBackgroundTaskTests
{
    [Fact]
    public async Task FilterPendingRowsAsync_PreparesTheFiltersOncePerBatch_AndKeepsOnlyTheRowsTheyAccept()
    {
        // Arrange
        // The batch hook was never called, so the duplicate check read every stored number up front instead of the
        // numbers of each batch, which ran a large import out of memory.
        var dataTable = new DataTable();
        dataTable.Columns.Add("Phone");

        var entry = new ContentTransferEntry();
        var progressPart = new ImportFileProcessStatsPart { Errors = [], ErrorMessages = [] };
        var pendingRows = Enumerable.Range(1, 3)
            .Select(rowIndex =>
            {
                var row = dataTable.NewRow();
                row["Phone"] = $"555-000-000{rowIndex}";

                return new ContentImportRowFilterContext { Row = row, Columns = dataTable.Columns, Entry = entry, RowIndex = rowIndex };
            })
            .ToList();

        var filter = new SkipRowFilter(skippedRowIndex: 2);
        var newRecords = new Dictionary<int, DataRow>();
        var existingRows = new Dictionary<string, KeyValuePair<int, DataRow>>(StringComparer.OrdinalIgnoreCase);

        // Act
        await ImportFilesBackgroundTask.FilterPendingRowsAsync(
            pendingRows,
            [filter],
            contentTypeDefinition: null,
            entry,
            progressPart,
            indexOfKeyColumn: -1,
            newRecords,
            existingRows,
            monitor: null,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, filter.PrepareCount);
        Assert.Equal([1, 2, 3], filter.PreparedRowIndexes);
        Assert.Equal([1, 3], newRecords.Keys.Order());
        Assert.Empty(existingRows);
        Assert.Empty(pendingRows);
        Assert.Equal("Skipped by the test filter.", progressPart.ErrorMessages[2]);
        Assert.Equal([2], progressPart.ErrorCategories["Test reason"]);
    }

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
    public void AddRowError_CountsEveryFailedRowByCategory_PastTheStoredMessages()
    {
        // Arrange
        // Only the first 1000 failed rows kept their reason, so a list with more failures could not tell how many
        // of its numbers were on a national do-not-call registry and how many were duplicates.
        var progressPart = new ImportFileProcessStatsPart();
        var registryRows = ImportFileProcessStatsPart.MaxStoredErrorMessages + 250;

        // Act
        for (var rowIndex = 1; rowIndex <= registryRows; rowIndex++)
        {
            ImportFilesBackgroundTask.AddRowError(progressPart, rowIndex, $"Row {rowIndex} is on a registry.", "On a national do-not-call registry");
        }

        for (var rowIndex = registryRows + 1; rowIndex <= registryRows + 40; rowIndex++)
        {
            ImportFilesBackgroundTask.AddRowError(progressPart, rowIndex, $"Row {rowIndex} is a duplicate.", "Already in the database");
        }

        ImportFilesBackgroundTask.AddRowError(progressPart, registryRows + 41, "No category.");

        // Assert
        Assert.Equal(registryRows + 41, progressPart.Errors.Count);
        Assert.Equal(ImportFileProcessStatsPart.MaxStoredErrorMessages, progressPart.ErrorMessages.Count);
        Assert.Equal(registryRows, progressPart.ErrorCategories["On a national do-not-call registry"].Count);
        Assert.Equal(40, progressPart.ErrorCategories["Already in the database"].Count);
        Assert.Contains(registryRows + 40, progressPart.ErrorCategories["Already in the database"]);
        Assert.Equal(2, progressPart.ErrorCategories.Count);
    }

    [Fact]
    public void AddRowError_MovesARowToTheCategoryItFailedWithLast()
    {
        // Arrange
        var progressPart = new ImportFileProcessStatsPart();
        ImportFilesBackgroundTask.AddRowError(progressPart, 7, "First attempt.", "Already in the database");

        // Act
        // A resumed run that reads the row again may fail it for another reason.
        ImportFilesBackgroundTask.AddRowError(progressPart, 7, "Second attempt.", "Did not pass validation");

        // Assert
        Assert.False(progressPart.ErrorCategories.ContainsKey("Already in the database"));
        Assert.Equal([7], progressPart.ErrorCategories["Did not pass validation"]);
    }

    [Fact]
    public void RemoveRowError_ForgetsTheRow_AndItsCategory()
    {
        // Arrange
        var progressPart = new ImportFileProcessStatsPart();
        ImportFilesBackgroundTask.AddRowError(progressPart, 3, "Failed.", "Did not pass validation");
        ImportFilesBackgroundTask.AddRowError(progressPart, 4, "Failed.", "Did not pass validation");

        // Act
        ImportFilesBackgroundTask.RemoveRowError(progressPart, 3);

        // Assert
        Assert.Equal([4], progressPart.Errors);
        Assert.False(progressPart.ErrorMessages.ContainsKey(3));
        Assert.Equal([4], progressPart.ErrorCategories["Did not pass validation"]);

        ImportFilesBackgroundTask.RemoveRowError(progressPart, 4);

        Assert.Empty(progressPart.ErrorCategories);
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
    public void RecordFailedRun_KeepsTheImportResuming_UntilTheSameRowHasFailedEveryAllowedRun()
    {
        var progressPart = new ImportFileProcessStatsPart { CurrentRow = 5_000 };

        for (var attempt = 1; attempt < ImportFilesBackgroundTask.MaxFailedRunAttempts; attempt++)
        {
            // A restart or a dropped connection fails a run or two; the import resumes by itself after each.
            Assert.False(ImportFilesBackgroundTask.RecordFailedRun(progressPart));
        }

        Assert.True(ImportFilesBackgroundTask.RecordFailedRun(progressPart));
        Assert.Equal(5_000, progressPart.FailedRunRow);
    }

    [Fact]
    public void RecordFailedRun_StartsOver_WhenTheImportSavedProgressSinceTheLastFailure()
    {
        var progressPart = new ImportFileProcessStatsPart { CurrentRow = 5_000 };

        ImportFilesBackgroundTask.RecordFailedRun(progressPart);
        ImportFilesBackgroundTask.RecordFailedRun(progressPart);

        // A batch was saved after the failures, so the import is making progress and the failures were passing.
        progressPart.CurrentRow = 5_100;

        Assert.False(ImportFilesBackgroundTask.RecordFailedRun(progressPart));
        Assert.Equal(5_100, progressPart.FailedRunRow);
        Assert.Equal(1, progressPart.FailedRunAttempts);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 120)]
    [InlineData(5, 120)]
    public void RetryBackoff_GrowsWithEachFailedRun_UpToTwoMinutes(int failedRuns, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ImportFilesBackgroundTask.RetryBackoff.GetDelay(failedRuns));
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

    private sealed class SkipRowFilter(int skippedRowIndex) : IContentImportRowFilter
    {
        public int PrepareCount { get; private set; }

        public List<int> PreparedRowIndexes { get; } = [];

        public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
            => Task.FromResult(true);

        public Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
        {
            PrepareCount++;
            PreparedRowIndexes.AddRange(context.Rows.Select(row => row.RowIndex));

            return Task.CompletedTask;
        }

        public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
        {
            if (context.RowIndex != skippedRowIndex)
            {
                return Task.FromResult(false);
            }

            context.SkipReason = "Skipped by the test filter.";
            context.SkipCategory = "Test reason";

            return Task.FromResult(true);
        }
    }
}
