using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The handlers that take part in writing a check: they complete or refuse the request before anything is saved, and do
/// their own work (such as posting the payment) after the check is saved, before the observers are told.
/// </summary>
public sealed class CheckWriteHandlerTests
{
    [Fact]
    public async Task IssueAsync_AHandler_CompletesTheRequest_AndIsToldOnceTheCheckIsSaved()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var handler = new RecordingWriteHandler(context)
        {
            OnWriting = writing =>
            {
                // The lines of the module's own editor give the amount, and the check pays its own record.
                writing.Request.Amount = 300m;
                writing.Request.SourceKind = "WrittenCheck";
                writing.Request.SourceId = "written1";
            },
        };
        context.WriteHandlers.Add(handler);

        // Act
        var result = await context.Service.IssueAsync(
            new CheckIssueRequest
            {
                BankAccountId = account.ItemId,
                PayeeName = "Pat Doe",
                CheckDate = new DateOnly(2026, 11, 13),
                Numbering = CheckNumbering.AtPrint,
                Properties = new Dictionary<string, object> { ["ExpenseLines"] = "lines" },
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal((300m, "WrittenCheck", "written1"), (result.Check.Amount, result.Check.SourceKind, result.Check.SourceId));
        Assert.Equal("lines", result.Check.Properties["ExpenseLines"]);
        var written = Assert.Single(handler.Written);
        Assert.Same(result.Check, written.Check);
        Assert.Null(written.ReissueOf);
        Assert.True(handler.SavedWhenWritten[0]);
        Assert.Equal(0, handler.ObserverChangesWhenWritten[0]);
        Assert.Single(context.Observer.Changes);
    }

    [Fact]
    public async Task IssueAsync_RefusedByAHandler_SavesNothing_AndSaysWhyWithBankingsOwnReasons()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var handler = new RecordingWriteHandler(context) { OnWriting = writing => writing.Refuse("The books are closed for November.") };
        context.WriteHandlers.Add(handler);

        // Act
        var result = await context.Service.IssueAsync(
            new CheckIssueRequest
            {
                BankAccountId = account.ItemId,
                PayeeName = "Pat Doe",
                Amount = 0m,
                CheckDate = new DateOnly(2026, 11, 13),
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The books are closed for November.", result.Errors[0]);
        Assert.Contains(result.Errors, error => error.Contains("more than zero", StringComparison.Ordinal));
        Assert.Empty(context.Store.Entries);
        Assert.Empty(handler.Written);
        Assert.Empty(context.Observer.Changes);
    }

    [Fact]
    public async Task IssueAsync_TheSameIdempotentRequestAgain_AsksNoHandler()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var handler = new RecordingWriteHandler(context);
        context.WriteHandlers.Add(handler);
        var request = new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat Doe",
            Amount = 10m,
            CheckDate = new DateOnly(2026, 11, 13),
            SourceKind = "Bill",
            SourceId = "bill1",
            IdempotencyKey = "pay-bill1",
        };
        var first = await context.Service.IssueAsync(request, TestContext.Current.CancellationToken);

        // Act
        var second = await context.Service.IssueAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(first.Check, second.Check);
        Assert.Single(handler.Writing);
        Assert.Single(handler.Written);
    }

    [Fact]
    public async Task ReissueAsync_TellsTheHandlersWhichCheckIsReplaced_AndCarriesItsProperties()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        check.Properties["ExpenseLines"] = "lines";
        var handler = new RecordingWriteHandler(context);
        context.WriteHandlers.Add(handler);

        // Act
        var result = await context.Service.ReissueAsync(check, "Lost", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Same(check, Assert.Single(handler.Writing).ReissueOf);
        Assert.Same(check, Assert.Single(handler.Written).ReissueOf);
        Assert.Equal("lines", result.Check.Properties["ExpenseLines"]);
    }

    [Fact]
    public async Task IssueAsync_AHandlerThatThrowsAfterTheCheckIsSaved_FailsTheRequest_AndTellsNoObserver()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        context.WriteHandlers.Add(new RecordingWriteHandler(context) { OnWritten = _ => throw new InvalidOperationException("The entry could not be posted.") });

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.IssueAsync(account));

        // Assert
        Assert.Equal("The entry could not be posted.", exception.Message);
        Assert.Empty(context.Observer.Changes);
    }

    private sealed class RecordingWriteHandler : CheckWriteHandlerBase
    {
        private readonly ChecksTestContext _context;

        public RecordingWriteHandler(ChecksTestContext context)
        {
            _context = context;
        }

        public Action<CheckWritingContext> OnWriting { get; init; }

        public Action<CheckWrittenContext> OnWritten { get; init; }

        public List<CheckWritingContext> Writing { get; } = [];

        public List<CheckWrittenContext> Written { get; } = [];

        public List<bool> SavedWhenWritten { get; } = [];

        public List<int> ObserverChangesWhenWritten { get; } = [];

        public override Task WritingAsync(CheckWritingContext context, CancellationToken cancellationToken = default)
        {
            Writing.Add(context);
            OnWriting?.Invoke(context);

            return Task.CompletedTask;
        }

        public override Task WrittenAsync(CheckWrittenContext context, CancellationToken cancellationToken = default)
        {
            Written.Add(context);
            SavedWhenWritten.Add(_context.Store.Entries.Contains(context.Check));
            ObserverChangesWhenWritten.Add(_context.Observer.Changes.Count);
            OnWritten?.Invoke(context);

            return Task.CompletedTask;
        }
    }
}
