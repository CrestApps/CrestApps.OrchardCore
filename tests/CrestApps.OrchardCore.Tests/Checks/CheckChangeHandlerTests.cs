using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The void date, the handlers asked before a void, reissue, clear or un-clear is saved (the module a check pays may
/// refuse), and un-clearing a check.
/// </summary>
public sealed class CheckChangeHandlerTests
{
    [Fact]
    public async Task VoidAsync_OnADate_StoresIt_AndGivesItToTheHandlersAndTheObservers()
    {
        // Arrange
        var context = new ChecksTestContext();
        var handler = new RecordingCheckChangeHandler();
        context.Handlers.Add(handler);
        var check = await PrintedAsync(context);
        context.Observer.Changes.Clear();

        // Act
        var result = await context.Service.VoidAsync(check, "Wrong amount", new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal((CheckStatus.Voided, new DateOnly(2026, 11, 20)), (check.Status, check.VoidedOn));
        var asked = Assert.Single(handler.Contexts);
        Assert.Equal((CheckChangeKind.Voided, CheckStatus.Voided, new DateOnly(2026, 11, 20), "Wrong amount"), (asked.Kind, asked.NewStatus, asked.VoidedOn, asked.Reason));
        Assert.Equal(CheckStatus.Printed, handler.StatusWhenAsked[0]);
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal((CheckChangeKind.Voided, new DateOnly(2026, 11, 20)), (change.Kind, change.VoidedOn));
    }

    [Fact]
    public async Task VoidAsync_WithoutADate_VoidsOnTodayInTheSitesTimeZone()
    {
        // Arrange: 15:00 UTC on November 13 is already November 14 at UTC+10.
        var context = new ChecksTestContext();
        context.LocalClock.Offset = TimeSpan.FromHours(10);
        var check = await PrintedAsync(context);

        // Act
        var voided = await context.Service.VoidAsync(check, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(voided);
        Assert.Equal(new DateOnly(2026, 11, 14), check.VoidedOn);
    }

    [Fact]
    public async Task VoidAsync_BeforeTheCheckDate_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);

        // Act
        var result = await context.Service.VoidAsync(check, null, check.CheckDate.AddDays(-1), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(CheckStatus.Printed, check.Status);
    }

    [Fact]
    public async Task VoidAsync_RefusedByAHandler_SavesNothing_AndSaysWhy()
    {
        // Arrange
        var context = new ChecksTestContext();
        context.Handlers.Add(new RecordingCheckChangeHandler("The books are closed for November."));
        var check = await PrintedAsync(context);
        context.Observer.Changes.Clear();

        // Act
        var result = await context.Service.VoidAsync(check, null, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);
        var voided = await context.Service.VoidAsync(check, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(["The books are closed for November."], result.Errors);
        Assert.False(voided);
        Assert.Equal(CheckStatus.Printed, check.Status);
        Assert.Null(check.VoidedOn);
        Assert.Empty(context.Observer.Changes);
    }

    [Fact]
    public async Task ReissueAsync_RefusedByAHandler_WritesNoNewCheck()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);
        context.Handlers.Add(new RecordingCheckChangeHandler("Refused."));

        // Act
        var result = await context.Service.ReissueAsync(check, "Lost", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Single(context.Store.Entries);
        Assert.Equal(CheckStatus.Printed, check.Status);
    }

    [Fact]
    public async Task ClearAsync_RefusedByAHandler_LeavesThePrintedCheck()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);
        var handler = new RecordingCheckChangeHandler("Refused.");
        context.Handlers.Add(handler);

        // Act
        var result = await context.Service.ClearAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);
        var cleared = await context.Service.MarkClearedAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(cleared);
        Assert.Equal(CheckStatus.Printed, check.Status);
        Assert.Null(check.ClearedOn);
        Assert.Equal(new DateOnly(2026, 11, 20), handler.Contexts[0].ClearedOn);
    }

    [Fact]
    public async Task StopPaymentAndStale_AreNotAskedAbout()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);
        context.Handlers.Add(new RecordingCheckChangeHandler("Refused."));

        // Act
        var stopped = await context.Service.StopPaymentAsync(check, "Lost", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(stopped);
    }

    [Fact]
    public async Task UnclearAsync_ACheckThatClearedFromPrinted_GoesBackToPrinted_AndTellsTheObservers()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);
        Assert.True((await context.Service.ClearAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken)).Succeeded);
        context.Observer.Changes.Clear();

        // Act
        var result = await context.Service.UnclearAsync(check, "Matched to the wrong check", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(CheckStatus.Printed, check.Status);
        Assert.Null(check.ClearedOn);
        Assert.Null(check.ClearedFrom);
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal((CheckChangeKind.Uncleared, CheckStatus.Cleared, new DateOnly(2026, 11, 20)), (change.Kind, change.PreviousStatus, change.ClearedOn));
    }

    [Fact]
    public async Task UnclearAsync_ACheckThatClearedWhileStale_GoesBackToStale()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);
        Assert.True(await context.Service.MarkStaleAsync(check, TestContext.Current.CancellationToken));
        Assert.True(await context.Service.MarkClearedAsync(check, new DateOnly(2027, 6, 1), TestContext.Current.CancellationToken));

        // Act
        var result = await context.Service.UnclearAsync(check, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(CheckStatus.Stale, check.Status);
    }

    [Fact]
    public async Task UnclearAsync_RefusedByAHandler_StaysCleared()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);
        Assert.True(await context.Service.MarkClearedAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken));
        var handler = new RecordingCheckChangeHandler("Reconciled.");
        context.Handlers.Add(handler);

        // Act
        var result = await context.Service.UnclearAsync(check, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(CheckStatus.Cleared, check.Status);
        Assert.Equal((CheckChangeKind.Uncleared, CheckStatus.Printed), (handler.Contexts[0].Kind, handler.Contexts[0].NewStatus));
    }

    [Fact]
    public async Task UnclearAsync_ACheckThatIsNotCleared_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await PrintedAsync(context);

        // Act
        var result = await context.Service.UnclearAsync(check, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(CheckStatus.Printed, check.Status);
    }

    private static async Task<CheckRecord> PrintedAsync(ChecksTestContext context)
    {
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);

        await context.Service.MarkPrintedAsync([check], "run1");

        return check;
    }

    private sealed class RecordingCheckChangeHandler : ICheckChangeHandler
    {
        private readonly string _refusal;

        public RecordingCheckChangeHandler(string refusal = null)
        {
            _refusal = refusal;
        }

        public List<CheckChangingContext> Contexts { get; } = [];

        public List<CheckStatus> StatusWhenAsked { get; } = [];

        public Task ChangingAsync(CheckChangingContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            StatusWhenAsked.Add(context.Check.Status);

            if (_refusal is not null)
            {
                context.Refuse(_refusal);
            }

            return Task.CompletedTask;
        }
    }
}
