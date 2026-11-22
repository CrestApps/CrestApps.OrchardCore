using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Models;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// How checks get their numbers: the next number of the series, a number given by a print run on pre-printed stock
/// (queued until then, misprints kept as spoiled checks), or the typed number of a check written by hand.
/// </summary>
public sealed class CheckNumberingTests
{
    [Fact]
    public async Task IssueAsync_AtPrint_QueuesTheCheckWithoutANumber_AndTakesNoNumberOfTheSeries()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();

        // Act
        var queued = await IssueAsync(context, account, CheckNumbering.AtPrint);
        var next = await context.IssueAsync(account);

        // Assert
        Assert.Equal(CheckStatus.Queued, queued.Status);
        Assert.Equal(0, queued.Number);
        Assert.Equal(CheckNumbering.AtPrint, queued.Numbering);
        Assert.Equal(CheckService.DefaultFirstNumber, next.Number);
        Assert.Equal(CheckChangeKind.Issued, context.Observer.Changes[0].Kind);
    }

    [Fact]
    public async Task IssueAsync_Handwritten_RecordsTheTypedNumberAsPrinted()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();

        // Act
        var check = await IssueAsync(context, account, CheckNumbering.Handwritten, 7001);

        // Assert
        Assert.Equal(7001, check.Number);
        Assert.Equal(CheckStatus.Printed, check.Status);
        Assert.Equal(CheckNumbering.Handwritten, check.Numbering);
        Assert.NotNull(check.PrintedUtc);
    }

    [Fact]
    public async Task IssueAsync_HandwrittenWithANumberTheAccountAlreadyUsed_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var printed = await context.IssueAsync(account);

        // Act
        var result = await context.Service.IssueAsync(Request(account, CheckNumbering.Handwritten, printed.Number), TestContext.Current.CancellationToken);
        var withoutNumber = await context.Service.IssueAsync(Request(account, CheckNumbering.Handwritten), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains(printed.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        Assert.False(withoutNumber.Succeeded);
        Assert.Single(context.Store.Entries);
    }

    [Fact]
    public async Task IssueAsync_TheSameNumberOnAnotherAccount_IsAllowed()
    {
        // Arrange
        var context = new ChecksTestContext();
        var operating = await context.BusinessAccountAsync("111122223333", "Operating");
        var payroll = await context.BusinessAccountAsync("444455556666", "Payroll");
        await IssueAsync(context, operating, CheckNumbering.Handwritten, 500);

        // Act
        var result = await context.Service.IssueAsync(Request(payroll, CheckNumbering.Handwritten, 500), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task IssueAsync_Next_SkipsANumberACheckWrittenByHandTook()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var first = await context.IssueAsync(account);
        await IssueAsync(context, account, CheckNumbering.Handwritten, first.Number + 1);

        // Act
        var next = await context.IssueAsync(account);

        // Assert
        Assert.Equal(first.Number + 2, next.Number);
    }

    [Fact]
    public async Task ConfirmPrintRunAsync_NumbersTheChecksFromTheFirstNumber_AndMovesTheSeriesPastThem()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var first = await IssueAsync(context, account, CheckNumbering.AtPrint, payee: "Pat");
        var second = await IssueAsync(context, account, CheckNumbering.AtPrint, payee: "Lee");
        context.Observer.Changes.Clear();

        // Act
        var result = await context.Service.ConfirmPrintRunAsync(new CheckPrintRunRequest { Checks = [first, second], FirstNumber = 5001, PrintRunId = "run1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal((5001L, CheckStatus.Printed, "run1"), (first.Number, first.Status, first.PrintRunId));
        Assert.Equal((5002L, CheckStatus.Printed), (second.Number, second.Status));
        Assert.Empty(result.Spoiled);
        Assert.All(context.Observer.Changes, change => Assert.Equal((CheckChangeKind.Printed, CheckStatus.Queued), (change.Kind, change.PreviousStatus)));
        Assert.Equal(5003, await context.Service.GetNextNumberAsync(account.ItemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConfirmPrintRunAsync_AMisprintedSheet_IsKeptAsASpoiledVoidedCheck_AndItsCheckWaitsToPrintAgain()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var first = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat",
            Amount = 900m,
            CheckDate = new DateOnly(2026, 11, 13),
            SourceKind = "PayStatement",
            SourceId = "statement-1",
            AccessScope = "employer-1",
            Numbering = CheckNumbering.AtPrint,
        }, TestContext.Current.CancellationToken);
        var second = await IssueAsync(context, account, CheckNumbering.AtPrint, payee: "Lee");
        context.Observer.Changes.Clear();

        // Act
        var result = await context.Service.ConfirmPrintRunAsync(new CheckPrintRunRequest
        {
            Checks = [first.Check, second],
            FirstNumber = 5001,
            MisprintedCheckIds = [first.Check.ItemId],
            PrintRunId = "run1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(CheckStatus.Queued, first.Check.Status);
        Assert.Equal(0, first.Check.Number);
        Assert.Equal(5002, second.Number);
        var spoiled = Assert.Single(result.Spoiled);
        Assert.Equal((5001L, CheckStatus.Voided, true), (spoiled.Number, spoiled.Status, spoiled.IsSpoiled));
        Assert.Equal(first.Check.ItemId, spoiled.SpoiledForId);
        Assert.Null(spoiled.SourceId);
        Assert.Equal(("PayStatement", "employer-1"), (spoiled.SourceKind, spoiled.AccessScope));
        Assert.Equal(new DateOnly(2026, 11, 13), spoiled.VoidedOn);
        Assert.Contains(context.Observer.Changes, change => change.Kind == CheckChangeKind.Spoiled && change.Check == spoiled);
        Assert.Single(await context.Manager.ListBySourceAsync("PayStatement", "statement-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConfirmPrintRunAsync_ChecksOfTwoAccounts_AreRefusedAndNothingIsSaved()
    {
        // Arrange
        var context = new ChecksTestContext();
        var operating = await context.BusinessAccountAsync("111122223333", "Operating");
        var payroll = await context.BusinessAccountAsync("444455556666", "Payroll");
        var first = await IssueAsync(context, operating, CheckNumbering.AtPrint);
        var second = await IssueAsync(context, payroll, CheckNumbering.AtPrint);

        // Act
        var result = await context.Service.ConfirmPrintRunAsync(new CheckPrintRunRequest { Checks = [first, second], FirstNumber = 5001 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal((CheckStatus.Queued, CheckStatus.Queued), (first.Status, second.Status));
    }

    [Fact]
    public async Task ConfirmPrintRunAsync_ANumberTheAccountAlreadyUsed_IsRefusedAndNothingIsSaved()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        await IssueAsync(context, account, CheckNumbering.Handwritten, 5002);
        var first = await IssueAsync(context, account, CheckNumbering.AtPrint);
        var second = await IssueAsync(context, account, CheckNumbering.AtPrint);

        // Act
        var result = await context.Service.ConfirmPrintRunAsync(new CheckPrintRunRequest { Checks = [first, second], FirstNumber = 5001 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("5002", StringComparison.Ordinal));
        Assert.Equal((0L, 0L), (first.Number, second.Number));
    }

    [Fact]
    public async Task ConfirmPrintRunAsync_ACheckThatIsNotQueued_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var issued = await context.IssueAsync(account);

        // Act
        var result = await context.Service.ConfirmPrintRunAsync(new CheckPrintRunRequest { Checks = [issued], FirstNumber = 5001 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(CheckStatus.Issued, issued.Status);
    }

    [Fact]
    public async Task MarkPrintedAsync_AQueuedCheck_StaysQueued()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var queued = await IssueAsync(context, account, CheckNumbering.AtPrint);

        // Act
        await context.Service.MarkPrintedAsync([queued], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CheckStatus.Queued, queued.Status);
    }

    [Fact]
    public async Task ReissueAsync_ACheckNumberedAtPrint_IsReplacedByAQueuedCheck()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var queued = await IssueAsync(context, account, CheckNumbering.AtPrint);
        await context.Service.ConfirmPrintRunAsync(new CheckPrintRunRequest { Checks = [queued], FirstNumber = 5001 }, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReissueAsync(queued, "Lost in the mail", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal((CheckStatus.Queued, 0L), (result.Check.Status, result.Check.Number));
        Assert.Equal(CheckStatus.Reissued, queued.Status);
    }

    [Fact]
    public async Task VoidAsync_AQueuedCheck_IsVoidedWithoutANumber()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var queued = await IssueAsync(context, account, CheckNumbering.AtPrint);

        // Act
        var voided = await context.Service.VoidAsync(queued, "Paid another way", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(voided);
        Assert.Equal((CheckStatus.Voided, 0L), (queued.Status, queued.Number));
    }

    [Fact]
    public async Task MarkReprintedAsync_ACheckWrittenByHand_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var handwritten = await IssueAsync(context, account, CheckNumbering.Handwritten, 300);

        // Act
        var reprinted = await context.Service.MarkReprintedAsync(handwritten, "run1", "Jam", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(reprinted);
    }

    private static async Task<CheckRecord> IssueAsync(ChecksTestContext context, BankAccount account, CheckNumbering numbering, long? number = null, string payee = "Pat Doe")
    {
        var result = await context.Service.IssueAsync(Request(account, numbering, number, payee));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Check;
    }

    private static CheckIssueRequest Request(BankAccount account, CheckNumbering numbering, long? number = null, string payee = "Pat Doe")
        => new()
        {
            BankAccountId = account.ItemId,
            PayeeName = payee,
            Amount = 250m,
            CheckDate = new DateOnly(2026, 11, 13),
            Numbering = numbering,
            Number = number,
        };
}
