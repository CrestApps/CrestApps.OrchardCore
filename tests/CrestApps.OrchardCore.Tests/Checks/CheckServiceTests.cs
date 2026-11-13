using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Tests.Checks;

public sealed class CheckServiceTests
{
    [Fact]
    public async Task IssueAsync_NumbersEachBankAccountsChecksInTheirOwnSeries()
    {
        // Arrange
        var context = new ChecksTestContext();
        var operating = await context.BusinessAccountAsync("111122223333", "Operating");
        var payroll = await context.BusinessAccountAsync("444455556666", "Payroll");

        // Act
        var first = await context.IssueAsync(operating);
        var second = await context.IssueAsync(operating);
        var other = await context.IssueAsync(payroll);

        // Assert
        Assert.Equal(CheckService.DefaultFirstNumber, first.Number);
        Assert.Equal(CheckService.DefaultFirstNumber + 1, second.Number);
        Assert.Equal(CheckService.DefaultFirstNumber, other.Number);
        Assert.Equal(CheckStatus.Issued, first.Status);
        Assert.Equal("Operating", first.BankAccountName);
        Assert.Equal("USD", first.Currency);
    }

    [Fact]
    public async Task SetNextNumberAsync_NewStock_SetsTheNextNumber()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        await context.IssueAsync(account);

        // Act
        var set = await context.Service.SetNextNumberAsync(account.ItemId, 5000, TestContext.Current.CancellationToken);
        var check = await context.IssueAsync(account);

        // Assert
        Assert.True(set);
        Assert.Equal(5000, check.Number);
        Assert.Equal(5001, await context.Service.GetNextNumberAsync(account.ItemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetNextNumberAsync_ANumberACheckAlreadyHas_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        await context.IssueAsync(account);
        await context.IssueAsync(account);

        // Act
        var set = await context.Service.SetNextNumberAsync(account.ItemId, CheckService.DefaultFirstNumber + 1, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(set);
        Assert.Equal(CheckService.DefaultFirstNumber + 2, await context.Service.GetNextNumberAsync(account.ItemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VoidAsync_KeepsTheNumber_AndTellsTheObservers()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);
        context.Observer.Changes.Clear();

        // Act
        var voided = await context.Service.VoidAsync(check, "Spoiled", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(voided);
        Assert.Equal(CheckStatus.Voided, check.Status);
        Assert.Equal(CheckService.DefaultFirstNumber, check.Number);
        Assert.Equal("Spoiled", check.StatusReason);
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal(CheckChangeKind.Voided, change.Kind);
        Assert.Equal(CheckStatus.Printed, change.PreviousStatus);
    }

    [Fact]
    public async Task ReissueAsync_ASpoiledCheck_GetsTheNextNumberAndLinksBothChecks()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account, 250m);
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ReissueAsync(check, "Jammed in the printer", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(CheckStatus.Reissued, check.Status);
        Assert.Equal(result.Check.ItemId, check.ReissuedById);
        Assert.Equal(check.ItemId, result.Check.ReissueOfId);
        Assert.Equal(check.Number + 1, result.Check.Number);
        Assert.Equal(250m, result.Check.Amount);
        Assert.Equal(CheckStatus.Issued, result.Check.Status);
    }

    [Theory]
    [InlineData(CheckStatus.Cleared)]
    [InlineData(CheckStatus.Voided)]
    public async Task VoidAndReissue_AFinalCheck_AreRefused(CheckStatus final)
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);

        if (final == CheckStatus.Cleared)
        {
            Assert.True(await context.Service.MarkClearedAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.True(await context.Service.VoidAsync(check, null, TestContext.Current.CancellationToken));
        }

        // Act
        var voided = await context.Service.VoidAsync(check, null, TestContext.Current.CancellationToken);
        var reissued = await context.Service.ReissueAsync(check, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(voided);
        Assert.False(reissued.Succeeded);
        Assert.Equal(final, check.Status);
    }

    [Fact]
    public async Task IssueAsync_OnAnAccountSomeoneElseOwns_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var staffAccount = await context.Banking.CreateAsync(owner: ChecksTestContext.Staff("s1"));

        // Act
        var result = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = staffAccount.ItemId,
            PayeeName = "Pat",
            Amount = 10,
            CheckDate = new DateOnly(2026, 11, 13),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Store.Entries);
    }

    [Fact]
    public async Task IssueAsync_AZeroAmountOrAClosedAccount_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var closed = await context.BusinessAccountAsync("99990000", "Old");
        await context.Banking.Service.ChangeStatusAsync(closed, BankAccountStatus.Closed, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var zero = await context.Service.IssueAsync(new CheckIssueRequest { BankAccountId = account.ItemId, PayeeName = "Pat", Amount = 0, CheckDate = new DateOnly(2026, 11, 13) }, TestContext.Current.CancellationToken);
        var onClosed = await context.Service.IssueAsync(new CheckIssueRequest { BankAccountId = closed.ItemId, PayeeName = "Pat", Amount = 5, CheckDate = new DateOnly(2026, 11, 13) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(zero.Succeeded);
        Assert.False(onClosed.Succeeded);
    }
}
