using System.Globalization;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// The texts of a pay stub, in the reader's language, so the document needs no localizer of its own.
/// </summary>
public sealed class PayStubTexts
{
    /// <summary>Gets or sets the title, such as "Pay statement".</summary>
    public string Title { get; set; } = "Pay statement";

    /// <summary>Gets or sets the label of the period.</summary>
    public string Period { get; set; } = "Period";

    /// <summary>Gets or sets the label of the pay date.</summary>
    public string PayDate { get; set; } = "Pay date";

    /// <summary>Gets or sets the label of the statement number.</summary>
    public string Number { get; set; } = "Statement";

    /// <summary>Gets or sets the column of the current amounts.</summary>
    public string Current { get; set; } = "Current";

    /// <summary>Gets or sets the column of the year's amounts.</summary>
    public string YearToDate { get; set; } = "Year to date";

    /// <summary>Gets or sets the column of the hours.</summary>
    public string Hours { get; set; } = "Hours";

    /// <summary>Gets or sets the label of gross pay.</summary>
    public string Gross { get; set; } = "Gross pay";

    /// <summary>Gets or sets the label of net pay.</summary>
    public string Net { get; set; } = "Net pay";

    /// <summary>Gets or sets the heading of the payments.</summary>
    public string Payments { get; set; } = "Paid by";

    /// <summary>Gets or sets the label of a deposit.</summary>
    public string Deposit { get; set; } = "Deposit";

    /// <summary>Gets or sets the label of a check.</summary>
    public string Check { get; set; } = "Check";

    /// <summary>Gets or sets the mark of a voided or reversing statement.</summary>
    public string Voided { get; set; } = "VOID";

    /// <summary>Gets or sets the mark of a statement not final yet, which has no number.</summary>
    public string Draft { get; set; } = "DRAFT";

    /// <summary>Gets or sets the heading of what the employer pays on top of pay, such as its taxes and contributions.</summary>
    public string EmployerPaid { get; set; } = "Paid by your employer";

    /// <summary>Gets or sets the heading of the balances, such as time off.</summary>
    public string Balances { get; set; } = "Balances";

    /// <summary>Gets or sets the unit of a balance in hours.</summary>
    public string HoursUnit { get; set; } = "hours";

    /// <summary>Gets or sets the unit of a balance in days.</summary>
    public string DaysUnit { get; set; } = "days";
}

/// <summary>
/// Writes a pay statement as a one-page PDF with the shared PDF writer: the employer and the person as printed, the
/// period, the lines with their year-to-date amounts, gross and net pay, and how net pay was paid. Helvetica only, so
/// it needs no font of the host.
/// </summary>
public static class PayStubDocument
{
    private const double Margin = 48;
    private const double LineHeight = 14;

    /// <summary>
    /// Writes a statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="codes">The pay codes' codes, by id.</param>
    /// <param name="accumulators">The year's totals at the statement's pay date, or <see langword="null"/>.</param>
    /// <param name="texts">The texts.</param>
    /// <returns>The PDF file.</returns>
    public static byte[] Write(PayStatement statement, IReadOnlyDictionary<string, string> codes, PayAccumulators accumulators, PayStubTexts texts)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(texts);

        using var writer = new PdfDocumentWriter(texts.Title);
        var regular = writer.AddStandardFont();
        var bold = writer.AddStandardFont(bold: true);
        var page = writer.AddPage(PdfPageSize.Letter);
        var right = PdfPageSize.Letter.Width - Margin;
        var y = Margin;

        // A stub is read by people: the reader's group and decimal separators, at the currency's precision.
        string Money(decimal value)
            => string.IsNullOrEmpty(statement.Currency) ? value.ToString("N2", CultureInfo.CurrentCulture) : CurrencyScale.FormatForDisplay(value, statement.Currency, CultureInfo.CurrentCulture);

        void Text(string text, PdfFontFace font, double size, double x, double top)
            => page.DrawText(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, x, top, PdfColor.Black);

        void TextRight(string text, PdfFontFace font, double size, double at, double top)
            => page.DrawTextRight(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, at, top, PdfColor.Black);

        Text(statement.Snapshot?.EmployerName, bold, 14, Margin, y);
        TextRight(texts.Title, bold, 14, right, y);
        y += LineHeight * 1.5;

        foreach (var line in statement.Snapshot?.EmployerAddress ?? [])
        {
            Text(line, regular, 9, Margin, y);
            y += LineHeight;
        }

        if (statement.VoidOf is not null || statement.Status == PayStatementStatus.Voided)
        {
            TextRight(texts.Voided, bold, 12, right, y);
        }
        else if (statement.Status == PayStatementStatus.Draft)
        {
            // A draft can still change: it says so, and has no number to show.
            TextRight(texts.Draft, bold, 12, right, y);
        }

        y += LineHeight;
        Text(statement.Snapshot?.EmployeeName, bold, 11, Margin, y);

        if (!string.IsNullOrEmpty(statement.Number))
        {
            TextRight($"{texts.Number} {statement.Number}", regular, 9, right, y);
        }
        y += LineHeight;

        foreach (var line in statement.Snapshot?.EmployeeAddress ?? [])
        {
            Text(line, regular, 9, Margin, y);
            y += LineHeight;
        }

        TextRight($"{texts.Period} {statement.PeriodStart.ToString("d", CultureInfo.CurrentCulture)} - {statement.PeriodEnd.ToString("d", CultureInfo.CurrentCulture)}", regular, 9, right, y);
        y += LineHeight;
        TextRight($"{texts.PayDate} {statement.PayDate.ToString("d", CultureInfo.CurrentCulture)}", regular, 9, right, y);
        y += LineHeight * 2;

        var hoursAt = right - 220;
        var currentAt = right - 110;

        Text(string.Empty, bold, 9, Margin, y);
        TextRight(texts.Hours, bold, 9, hoursAt, y);
        TextRight(texts.Current, bold, 9, currentAt, y);

        // A draft is in no year-to-date total yet: no empty column.
        if (accumulators is not null)
        {
            TextRight(texts.YearToDate, bold, 9, right, y);
        }
        y += 4;
        page.DrawLine(Margin, y + LineHeight - 4, right, y + LineHeight - 4, PdfColor.Black, 0.5);
        y += LineHeight;

        // What the employer pays on top of pay (its taxes and contributions) is not taken from the person's pay: it is
        // listed apart, after net pay, so it isn't read as earnings.
        static bool PaidByEmployer(PayStatementLine line)
            => line.Type == PayLineType.EmployerContribution || line.Type == PayLineType.Tax && line.Payer == PayLinePayer.Employer;

        void Line(PayStatementLine line)
        {
            var key = line.Type == PayLineType.Tax ? PayAccumulators.TaxKey(line.TaxCode) : PayAccumulators.CodeKey(line.PayCodeId);
            var code = line.PayCodeId is not null && codes is not null && codes.TryGetValue(line.PayCodeId, out var text) ? text : line.TaxCode;
            var label = string.IsNullOrEmpty(line.Description) ? code : $"{code} {line.Description}";

            Text(PdfText.Fit(PdfText.Clean(label), regular, 9, hoursAt - Margin - 70), regular, 9, Margin, y);

            if (line.Hours is { } hours)
            {
                TextRight(hours.ToString("0.##", CultureInfo.CurrentCulture), regular, 9, hoursAt, y);
            }

            var sign = line.Type is PayLineType.Deduction or PayLineType.Tax && line.Payer == PayLinePayer.Employee ? -1 : 1;

            TextRight(Money(sign * line.Amount), regular, 9, currentAt, y);

            if (accumulators is not null)
            {
                TextRight(Money(sign * accumulators.For(key).Year), regular, 9, right, y);
            }

            y += LineHeight;
        }

        foreach (var line in statement.Lines.Where(line => !PaidByEmployer(line)))
        {
            Line(line);

            if (y > PdfPageSize.Letter.Height - 160)
            {
                break;
            }
        }

        page.DrawLine(Margin, y, right, y, PdfColor.Black, 0.5);
        y += LineHeight / 2;
        Text(texts.Gross, bold, 10, Margin, y);
        TextRight(Money(statement.Gross), bold, 10, currentAt, y);
        y += LineHeight;
        Text(texts.Net, bold, 11, Margin, y);
        TextRight(Money(statement.Net), bold, 11, currentAt, y);
        y += LineHeight * 2;

        if (statement.Payments.Count > 0)
        {
            Text(texts.Payments, bold, 9, Margin, y);
            y += LineHeight;

            foreach (var payment in statement.Payments)
            {
                var label = payment.Method == NetPayMethod.BankAccount
                    ? string.IsNullOrEmpty(payment.Reference) ? texts.Deposit : $"{texts.Deposit} {payment.Reference}"
                    : string.IsNullOrEmpty(payment.Reference) ? texts.Check : payment.Reference;

                Text(PdfText.Fit(PdfText.Clean(label), regular, 9, currentAt - Margin - 90), regular, 9, Margin, y);
                TextRight(Money(payment.Amount), regular, 9, currentAt, y);
                y += LineHeight;
            }
        }

        var employerPaid = statement.Lines.Where(PaidByEmployer).ToList();

        if (employerPaid.Count > 0 && y < PdfPageSize.Letter.Height - 120)
        {
            y += LineHeight / 2;
            Text(texts.EmployerPaid, bold, 9, Margin, y);
            y += LineHeight;

            foreach (var line in employerPaid)
            {
                Line(line);

                if (y > PdfPageSize.Letter.Height - 80)
                {
                    break;
                }
            }
        }

        if (statement.Balances.Count > 0)
        {
            y += LineHeight / 2;
            Text(texts.Balances, bold, 9, Margin, y);
            y += LineHeight;

            foreach (var balance in statement.Balances)
            {
                var unit = balance.Unit == PayStatementBalance.Days ? texts.DaysUnit : texts.HoursUnit;

                Text(PdfText.Fit(PdfText.Clean(balance.Name), regular, 9, currentAt - Margin - 90), regular, 9, Margin, y);
                TextRight(PdfText.Clean($"{balance.Balance.ToString("0.##", CultureInfo.CurrentCulture)} {unit}"), regular, 9, currentAt, y);
                y += LineHeight;
            }
        }

        return writer.Build();
    }
}
