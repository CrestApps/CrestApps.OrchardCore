using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Pdf;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Checks;

public sealed class CheckPrinterTests
{
    [Fact]
    public async Task PrintAsync_ALayoutThatPrintsTheCheckLineWithoutAFont_SaysSo()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        var layout = new CheckLayout { ItemId = "layout1", Name = "Blank", PrintMicr = true };

        // Act
        var result = await context.Printer().PrintAsync(layout, [check], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("check line font", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CheckForm.CheckOnTop, 4, 4)]
    [InlineData(CheckForm.ThreePerPage, 4, 2)]
    public async Task PrintAsync_PutsOneOrThreeChecksOnASheet(CheckForm form, int checkCount, int pageCount)
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var checks = new List<CheckRecord>();

        for (var i = 0; i < checkCount; i++)
        {
            checks.Add(await context.IssueAsync(account, 100 + i));
        }

        var layout = new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted, Form = form };

        // Act
        var result = await context.Printer().PrintAsync(layout, checks, "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        using var document = PdfDocument.Open(result.Pdf);
        Assert.Equal(pageCount, document.NumberOfPages);
        var text = document.GetPage(1).Text;
        Assert.Contains("One hundred and 00/100", text, StringComparison.Ordinal);
        Assert.Contains(CheckService.DefaultFirstNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintAsync_ACheckOfAModule_PrintsTheVoucherTheModuleSupplies()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var result = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat Doe",
            Amount = 812.40m,
            CheckDate = new DateOnly(2026, 11, 13),
            SourceKind = "Test",
            SourceId = "s1",
        }, TestContext.Current.CancellationToken);
        var layout = new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted };

        // Act
        var printed = await context.Printer(new TestVoucherSource()).PrintAsync(layout, [result.Check], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(printed.Succeeded, string.Join(" ", printed.Errors));
        using var document = PdfDocument.Open(printed.Pdf);
        var text = document.GetPage(1).Text;
        Assert.Contains("Regular pay", text, StringComparison.Ordinal);
        Assert.Contains("Federal income tax", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintAsync_TheCheckLine_PrintsEachCharacterAtItsStandardPosition()
    {
        var standIn = PdfFontSet.GetInstalled("Arial").Regular;
        Assert.SkipWhen(standIn is null, "No TrueType font is installed to stand in for a MICR font.");

        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync("123456789");
        var check = await context.IssueAsync(account);
        var layout = new CheckLayout { ItemId = "layout1", Name = "Blank", PrintMicr = true, HasMicrFont = true, MicrSymbolCharacters = "ABCD" };
        await context.Files.WriteAsync("layout1", ChecksConstants.LayoutFiles.MicrFont, standIn);
        context.Banking.Observer.Changes.Clear();

        // Act
        var result = await context.Printer().PrintAsync(layout, [check], "run1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        using var document = PdfDocument.Open(result.Pdf);
        var page = document.GetPage(1);
        var expected = new UnitedStatesCheckMicrFormat().Format(new MicrLineRequest { RoutingNumber = account.RoutingNumber, AccountNumber = "123456789", CheckNumber = check.Number });

        // The check line is the lowest text of the check: the bottom of the top third of the sheet.
        var baseline = page.Height - (CheckLayoutDefaults.CheckHeight - CheckLayoutDefaults.MicrBaselineFromBottom);
        var micrLetters = page.Letters.Where(letter => Math.Abs(letter.StartBaseLine.Y - baseline) < 0.5).ToList();

        Assert.Equal(expected.Characters.Count, micrLetters.Count);

        foreach (var character in expected.Characters)
        {
            var glyph = MicrSymbols.ToFont(character.Character.ToString(), "ABCD");
            var x = MicrLine.LeftOf(character.Position, CheckLayoutDefaults.CheckWidth);

            Assert.Contains(micrLetters, letter => letter.Value == glyph && Math.Abs(letter.StartBaseLine.X - x) < 0.5);
        }

        // Reading the account number for the check line is recorded as a reveal.
        Assert.Contains(context.Banking.Observer.Changes, change => change.Has(BankAccountChangeKind.NumberRevealed));
    }

    private sealed class TestVoucherSource : ICheckVoucherSource
    {
        public string SourceKind => "Test";

        public Task<CheckVoucher> GetVoucherAsync(CheckRecord check, CancellationToken cancellationToken = default)
            => Task.FromResult(new CheckVoucher
            {
                Title = "Pay statement 1",
                Columns = ["Item", "Current", "Year to date"],
                Lines =
                [
                    new CheckVoucherLine { Label = "Regular pay", Values = ["1,000.00", "20,000.00"] },
                    new CheckVoucherLine { Label = "Federal income tax", Values = ["-187.60", "-3,752.00"] },
                    new CheckVoucherLine { Label = "Net pay", Values = ["812.40", "16,248.00"], IsEmphasized = true },
                ],
            });
    }
}
