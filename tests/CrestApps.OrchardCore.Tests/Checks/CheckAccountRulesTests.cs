using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Models;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// Which of the business's accounts checks are drawn on: any checking or savings account, with or without its numbers
/// until the check line needs them.
/// </summary>
public sealed class CheckAccountRulesTests
{
    [Fact]
    public async Task IssueAsync_ABusinessAccountWithoutNumbers_IsIssued()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await NumberlessAccountAsync(context);

        // Act
        var result = await context.Service.IssueAsync(Request(account), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    [Theory]
    [InlineData(BankAccountKind.CreditCard)]
    [InlineData(BankAccountKind.LineOfCredit)]
    [InlineData(BankAccountKind.Cash)]
    [InlineData(BankAccountKind.Other)]
    public async Task IssueAsync_AnAccountThatIsNotCheckingOrSavings_IsRefused(BankAccountKind kind)
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        account.Kind = kind;
        await context.Banking.Manager.UpdateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.IssueAsync(Request(account), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Store.Entries);
    }

    [Fact]
    public async Task PrintAsync_TheCheckLineOfAnAccountWithoutNumbers_IsRefusedWithTheAccountsName()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await NumberlessAccountAsync(context);
        var check = (await context.Service.IssueAsync(Request(account), TestContext.Current.CancellationToken)).Check;
        var layout = new CheckLayout { ItemId = "layout1", Name = "Blank", PrintMicr = true, HasMicrFont = true };
        await context.Files.WriteAsync("layout1", ChecksConstants.LayoutFiles.MicrFont, [1, 2, 3]);

        // Act
        var result = await context.Printer().PrintAsync(layout, [check], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("Petty checking", StringComparison.Ordinal) && error.Contains("routing or account number", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintAsync_PrePrintedStockWithoutTheCheckLine_PrintsForAnAccountWithoutNumbers()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await NumberlessAccountAsync(context);
        var check = (await context.Service.IssueAsync(Request(account), TestContext.Current.CancellationToken)).Check;
        var layout = new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted };

        // Act
        var result = await context.Printer().PrintAsync(layout, [check], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        using var document = PdfDocument.Open(result.Pdf);
        Assert.Contains("Pat Doe", document.GetPage(1).Text, StringComparison.Ordinal);
    }

    private static async Task<BankAccount> NumberlessAccountAsync(ChecksTestContext context)
    {
        var account = await context.Banking.Manager.NewAsync();

        account.Name = "Petty checking";
        account.HolderName = "Contoso Ltd";
        account.AccountNumberLastFour = "1234";
        await context.Banking.Manager.CreateAsync(account);

        return account;
    }

    private static CheckIssueRequest Request(BankAccount account)
        => new()
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat Doe",
            Amount = 125m,
            CheckDate = new DateOnly(2026, 11, 13),
        };
}
