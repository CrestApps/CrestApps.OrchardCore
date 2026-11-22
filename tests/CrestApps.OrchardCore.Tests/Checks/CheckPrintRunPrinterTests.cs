using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Banking.Models;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// Printing queued checks with the numbers of the sheets they print on, the rules of a run on pre-printed stock, and
/// vouchers of several sections such as a pay stub.
/// </summary>
public sealed class CheckPrintRunPrinterTests
{
    private static readonly CheckLayout _prePrinted = new() { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted, Form = CheckForm.ThreePerPage };

    [Fact]
    public async Task PrintAsync_WithAFirstNumber_PrintsEachQueuedCheckWithItsSheetsNumber_AndSavesNoNumber()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var first = await QueueAsync(context, account, "Pat Doe");
        var second = await QueueAsync(context, account, "Lee Roe");

        // Act
        var result = await context.Printer().PrintAsync(_prePrinted, [first, second], "run1", 5001, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        using var document = PdfDocument.Open(result.Pdf);
        var text = document.GetPage(1).Text;
        Assert.Contains("5001", text, StringComparison.Ordinal);
        Assert.Contains("5002", text, StringComparison.Ordinal);
        Assert.Equal((0L, 0L), (first.Number, second.Number));
        Assert.Equal(CheckStatus.Queued, first.Status);
    }

    [Fact]
    public async Task PrintAsync_AQueuedCheckWithoutAFirstNumber_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var queued = await QueueAsync(context, account, "Pat Doe");

        // Act
        var result = await context.Printer().PrintAsync(_prePrinted, [queued], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("print run", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintAsync_AFirstNumberForACheckThatHasOne_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var issued = await context.IssueAsync(account);

        // Act
        var result = await context.Printer().PrintAsync(_prePrinted, [issued], "run1", 5001, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PrintAsync_PrePrintedStock_ChecksOfTwoAccounts_AreRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var operating = await context.BusinessAccountAsync("111122223333", "Operating");
        var payroll = await context.BusinessAccountAsync("444455556666", "Payroll");
        var first = await context.IssueAsync(operating);
        var second = await context.IssueAsync(payroll);

        // Act
        var result = await context.Printer().PrintAsync(_prePrinted, [first, second], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("one bank account", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintAsync_ACheckWrittenByHand_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var handwritten = (await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat",
            Amount = 10m,
            CheckDate = new DateOnly(2026, 11, 13),
            Numbering = CheckNumbering.Handwritten,
            Number = 42,
        }, TestContext.Current.CancellationToken)).Check;

        // Act
        var result = await context.Printer().PrintAsync(_prePrinted, [handwritten], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("by hand", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintAsync_AVoucherOfSections_PrintsEachSectionsTitleColumnsAndRows()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var result = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat Doe",
            Amount = 1521.30m,
            CheckDate = new DateOnly(2026, 11, 13),
            SourceKind = "Stub",
            SourceId = "s1",
        }, TestContext.Current.CancellationToken);
        var layout = new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted, Form = CheckForm.CheckOnTop };

        // Act
        var printed = await context.Printer(new PayStubVoucherSource()).PrintAsync(layout, [result.Check], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(printed.Succeeded, string.Join(" ", printed.Errors));
        using var document = PdfDocument.Open(printed.Pdf);
        var text = document.GetPage(1).Text;

        foreach (var expected in new[] { "Earnings", "Rate", "Year to date", "Overtime", "Taxes", "Social Security", "Deductions", "Health plan", "Net pay", "1,521.30" })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("More lines than the voucher holds", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSections_AVoucherOfOneTable_IsOneSectionWithoutATitle_ThenItsSections()
    {
        // Arrange
        var voucher = new CheckVoucher
        {
            Columns = ["Item", "Amount"],
            Lines = [new CheckVoucherLine { Label = "Invoice 12", Values = ["10.00"] }],
            Sections = [new CheckVoucherSection { Title = "Discounts", Columns = ["Item", "Amount"] }],
        };

        // Act
        var sections = voucher.GetSections();

        // Assert
        Assert.Equal(2, sections.Count);
        Assert.Null(sections[0].Title);
        Assert.Equal("Invoice 12", Assert.Single(sections[0].Lines).Label);
        Assert.Equal("Discounts", sections[1].Title);
    }

    private static async Task<CheckRecord> QueueAsync(ChecksTestContext context, BankAccount account, string payee)
    {
        var result = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = payee,
            Amount = 100m,
            CheckDate = new DateOnly(2026, 11, 13),
            Numbering = CheckNumbering.AtPrint,
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Check;
    }

    private sealed class PayStubVoucherSource : ICheckVoucherSource
    {
        public string SourceKind => "Stub";

        public Task<CheckVoucher> GetVoucherAsync(CheckRecord check, CancellationToken cancellationToken = default)
            => Task.FromResult(new CheckVoucher
            {
                Title = "Pay statement 7, period 11/01 to 11/15",
                Sections =
                [
                    new CheckVoucherSection
                    {
                        Title = "Earnings",
                        Columns = ["Earning", "Hours", "Rate", "Current", "Year to date"],
                        Lines =
                        [
                            new CheckVoucherLine { Label = "Regular", Values = ["80.00", "22.50", "1,800.00", "37,800.00"] },
                            new CheckVoucherLine { Label = "Overtime", Values = ["4.00", "33.75", "135.00", "1,012.50"] },
                            new CheckVoucherLine { Label = "Gross pay", Values = [string.Empty, string.Empty, "1,935.00", "38,812.50"], IsEmphasized = true },
                        ],
                    },
                    new CheckVoucherSection
                    {
                        Title = "Taxes",
                        Columns = ["Tax", "Current", "Year to date"],
                        Lines =
                        [
                            new CheckVoucherLine { Label = "Federal income tax", Values = ["-190.00", "-3,990.00"] },
                            new CheckVoucherLine { Label = "Social Security", Values = ["-119.97", "-2,406.38"] },
                            new CheckVoucherLine { Label = "Medicare", Values = ["-28.06", "-562.78"] },
                        ],
                    },
                    new CheckVoucherSection
                    {
                        Title = "Deductions",
                        Columns = ["Deduction", "Current", "Year to date"],
                        Lines =
                        [
                            new CheckVoucherLine { Label = "Health plan", Values = ["-75.67", "-1,589.07"] },
                        ],
                    },
                    new CheckVoucherSection
                    {
                        Lines = [new CheckVoucherLine { Label = "Net pay", Values = ["1,521.30", "30,264.27"], IsEmphasized = true }],
                    },
                ],
            });
    }
}
