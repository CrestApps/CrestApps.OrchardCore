using System.Globalization;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The pay stub reads as the person's: a draft says so, what the employer pays on top of pay is listed apart after net
/// pay, and amounts carry the reader's separators.
/// </summary>
public sealed class PayStubDocumentTests
{
    [Fact]
    public void ADraft_IsMarked_HasNoEmptyColumnOrNumber_AndListsWhatTheEmployerPaysApart()
    {
        // Arrange
        var statement = new PayStatement
        {
            Status = PayStatementStatus.Draft,
            Currency = "USD",
            PayDate = new DateOnly(2026, 11, 27),
            PeriodStart = new DateOnly(2026, 11, 8),
            PeriodEnd = new DateOnly(2026, 11, 21),
            Gross = 12500,
            Net = 11000,
            Lines =
            [
                new PayStatementLine { Type = PayLineType.Earning, Description = "Salary", Amount = 12500 },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS-EE", Description = "Social Security", Amount = 775, Payer = PayLinePayer.Employee },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS-ER", Description = "Social Security (employer)", Amount = 775, Payer = PayLinePayer.Employer },
                new PayStatementLine { Type = PayLineType.EmployerContribution, Description = "401(k) employer match", Amount = 62.5m, Payer = PayLinePayer.Employer },
            ],
            Snapshot = new PayStatementSnapshot { EmployerName = "Contoso, Inc.", EmployeeName = "Riley Chen" },
        };

        // Act
        string text;

        using (new CultureScope(CultureInfo.GetCultureInfo("en-US")))
        {
            var pdf = PayStubDocument.Write(statement, new Dictionary<string, string>(), accumulators: null, new PayStubTexts());
            using var document = PdfDocument.Open(pdf);
            text = string.Join(" ", document.GetPage(1).GetWords().Select(word => word.Text));
        }

        // Assert
        Assert.Contains("DRAFT", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Statement", text.Replace("Pay statement", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("Year to date", text, StringComparison.Ordinal);
        Assert.Contains("12,500.00", text, StringComparison.Ordinal);

        var employerSection = text.IndexOf("Paid by your employer", StringComparison.Ordinal);

        Assert.True(employerSection > text.IndexOf("Net pay", StringComparison.Ordinal), text);
        Assert.True(text.IndexOf("(employer)", StringComparison.Ordinal) > employerSection, text);
        Assert.True(text.IndexOf("match", StringComparison.Ordinal) > employerSection, text);
        Assert.True(text.IndexOf("US-SS-EE", StringComparison.Ordinal) < employerSection, text);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

        public CultureScope(CultureInfo culture)
            => CultureInfo.CurrentCulture = culture;

        public void Dispose()
            => CultureInfo.CurrentCulture = _culture;
    }
}
