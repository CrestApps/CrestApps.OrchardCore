using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Checks;
using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Pdf;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The rules the review of the Checks building block asked for: local dates, a print run that checks every check line
/// first and reads each account number once, explicit reprints, stale checks, and the status corrections.
/// </summary>
public sealed class CheckReviewFixesTests
{
    [Fact]
    public async Task ReissueAsync_LateEveningInTheSitesZone_DatesTheNewCheckWithTheLocalDay()
    {
        // Arrange: 03:30 UTC on the 14th is still the evening of the 13th in a United States zone.
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        context.Banking.Now = new DateTime(2026, 11, 14, 3, 30, 0, DateTimeKind.Utc);
        context.LocalClock.Offset = TimeSpan.FromHours(-5);

        // Act
        var result = await context.Service.ReissueAsync(check, "Lost", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(new DateOnly(2026, 11, 13), result.Check.CheckDate);
    }

    [Fact]
    public async Task ReissueAsync_KeepsTheAccessScopeOfTheReplacedCheck()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var issued = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat Doe",
            Amount = 10,
            CheckDate = new DateOnly(2026, 11, 13),
            SourceKind = "Paychecks",
            SourceId = "statement1",
            AccessScope = "groupA",
        }, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReissueAsync(issued.Check, "Lost", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("groupA", issued.Check.AccessScope);
        Assert.Equal("groupA", result.Check.AccessScope);
    }

    [Fact]
    public async Task IssueAsync_AnAccessScopeWithoutASourceKind_IsDropped()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();

        // Act
        var result = await context.Service.IssueAsync(new CheckIssueRequest { BankAccountId = account.ItemId, PayeeName = "Pat", Amount = 5, CheckDate = new DateOnly(2026, 11, 13), AccessScope = "groupA" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.Check.AccessScope);
    }

    [Fact]
    public async Task PrintAsync_ReadsEachAccountNumberOncePerRun_NamingTheChecksAndTheRun()
    {
        var standIn = PdfFontSet.GetInstalled("Arial").Regular;
        Assert.SkipWhen(standIn is null, "No TrueType font is installed to stand in for a MICR font.");

        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync("123456789");
        var checks = new List<CheckRecord> { await context.IssueAsync(account), await context.IssueAsync(account), await context.IssueAsync(account) };
        var layout = await MicrLayoutAsync(context, standIn);
        context.Banking.Observer.Changes.Clear();

        // Act
        var result = await context.Printer().PrintAsync(layout, checks, "run42", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        var reveal = Assert.Single(context.Banking.Observer.Changes, change => change.Has(BankAccountChangeKind.NumberRevealed));
        Assert.Contains("1001", reveal.Reason, StringComparison.Ordinal);
        Assert.Contains("1003", reveal.Reason, StringComparison.Ordinal);
        Assert.Contains("run42", reveal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintAsync_AnAccountNumberTheCheckLineCannotHold_RefusesTheRun_NamingTheAccount()
    {
        var standIn = PdfFontSet.GetInstalled("Arial").Regular;
        Assert.SkipWhen(standIn is null, "No TrueType font is installed to stand in for a MICR font.");

        // Arrange: an account number with no digit, which the check line cannot print.
        var context = new ChecksTestContext();
        var good = await context.BusinessAccountAsync("123456789", "Operating");
        var bad = await context.BusinessAccountAsync("ABCDEFGH", "Overflow");
        var checks = new List<CheckRecord> { await context.IssueAsync(good), await context.IssueAsync(bad) };
        var layout = await MicrLayoutAsync(context, standIn);

        // Act
        var result = await context.Printer().PrintAsync(layout, checks, "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Pdf);
        Assert.Contains(result.Errors, error => error.Contains("Overflow", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintAsync_TheAmountLine_WritesTheCurrencyName()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account, 100m);

        // Act
        var result = await context.Printer().PrintAsync(new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted }, [check], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        using var document = PdfDocument.Open(result.Pdf);
        var text = string.Join(" ", document.GetPages().Select(page => page.Text));
        Assert.Contains("00/100Dollars", text.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("00/100USD", text.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkPrintedAsync_APrintedCheck_IsNotPrintedAgain_ButAReprintIsRecordedWithItsReason()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);
        context.Observer.Changes.Clear();

        // Act
        await context.Service.MarkPrintedAsync([check], "run2", TestContext.Current.CancellationToken);
        var withoutReason = await context.Service.MarkReprintedAsync(check, "run3", " ", TestContext.Current.CancellationToken);
        var reprinted = await context.Service.MarkReprintedAsync(check, "run4", "Printer jam", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(withoutReason);
        Assert.True(reprinted);
        Assert.Equal("run4", check.PrintRunId);
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal(CheckChangeKind.Reprinted, change.Kind);
        Assert.Equal("Printer jam", change.Reason);
    }

    [Fact]
    public async Task MarkStaleChecksAsync_MarksOnlyPrintedChecksOlderThanTheAge()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var old = await IssueDatedAsync(context, account, new DateOnly(2026, 4, 1));
        var recent = await IssueDatedAsync(context, account, new DateOnly(2026, 10, 1));
        var unprinted = await IssueDatedAsync(context, account, new DateOnly(2026, 3, 1));
        await context.Service.MarkPrintedAsync([old, recent], "run1", TestContext.Current.CancellationToken);

        // Act
        var count = await context.Service.MarkStaleChecksAsync(180, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(CheckStatus.Stale, old.Status);
        Assert.Equal(CheckStatus.Printed, recent.Status);
        Assert.Equal(CheckStatus.Issued, unprinted.Status);
        Assert.Equal(CheckChangeKind.Stale, context.Observer.Changes[^1].Kind);
    }

    [Fact]
    public async Task MarkClearedAsync_ARefusedMove_LeavesTheClearedDateEmpty()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        await context.Service.VoidAsync(check, "Spoiled", TestContext.Current.CancellationToken);

        // Act
        var cleared = await context.Service.MarkClearedAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(cleared);
        Assert.Null(check.ClearedOn);
    }

    [Fact]
    public async Task AReissuedCheckTheBankPaid_CanBeRecordedAsCleared()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);
        await context.Service.ReissueAsync(check, "Lost", TestContext.Current.CancellationToken);

        // Act
        var cleared = await context.Service.MarkClearedAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(cleared);
        Assert.Equal(CheckStatus.Cleared, check.Status);
        Assert.NotNull(check.ReissuedById);
        Assert.Equal(new DateOnly(2026, 11, 20), check.ClearedOn);
    }

    [Fact]
    public async Task AHistoricalCheck_CanClearOrBeVoided_ButNeverPrintsOrIsReissued()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var printed = await context.IssueAsync(account);
        var outstanding = await context.IssueAsync(account);
        await context.Service.MarkPrintedAsync([printed, outstanding], "run1", TestContext.Current.CancellationToken);
        printed.IsHistorical = true;
        outstanding.IsHistorical = true;

        // Act
        var reissue = await context.Service.ReissueAsync(outstanding, "Lost", TestContext.Current.CancellationToken);
        var reprint = await context.Service.MarkReprintedAsync(outstanding, "run2", "Jam", TestContext.Current.CancellationToken);
        var cleared = await context.Service.MarkClearedAsync(printed, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);
        var voided = await context.Service.VoidAsync(outstanding, "Never cashed", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(reissue.Succeeded);
        Assert.False(reprint);
        Assert.True(cleared);
        Assert.True(voided);
    }

    [Fact]
    public async Task ANewCheck_IsStampedWithWhenItChanged()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();

        // Act
        var check = await context.IssueAsync(account);

        // Assert
        Assert.Equal(context.Banking.Now, check.ModifiedUtc);
    }

    [Fact]
    public async Task DeletingABankAccountChecksAreDrawnOn_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        await context.IssueAsync(account);

        // Act
        var usages = await context.Banking.Service.GetUsagesAsync(account, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Checks are drawn on it."], usages);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await context.Banking.Manager.DeleteAsync(account, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("USD", "One hundred and 00/100 Dollars")]
    [InlineData("EUR", "One hundred and 00/100 Euros")]
    [InlineData("XYZ", "One hundred and 00/100 XYZ")]
    [InlineData(null, "One hundred and 00/100")]
    public void AmountInWords_WritesTheCurrencyName(string currency, string expected)
        => Assert.Equal(expected, new EnglishAmountInWords().Write(100m, 2, currency));

    private static async Task<CheckRecord> IssueDatedAsync(ChecksTestContext context, BankAccount account, DateOnly date)
    {
        var result = await context.Service.IssueAsync(new CheckIssueRequest { BankAccountId = account.ItemId, PayeeName = "Pat", Amount = 5, CheckDate = date }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Check;
    }

    private static async Task<CheckLayout> MicrLayoutAsync(ChecksTestContext context, byte[] font)
    {
        var layout = new CheckLayout { ItemId = "layout1", Name = "Blank", PrintMicr = true, HasMicrFont = true, MicrSymbolCharacters = "ABCD", MicrCountry = "US" };

        await context.Files.WriteAsync("layout1", ChecksConstants.LayoutFiles.MicrFont, font);

        return layout;
    }
}
