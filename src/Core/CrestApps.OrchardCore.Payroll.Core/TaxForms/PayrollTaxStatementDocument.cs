using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.Payroll.Core.TaxForms;

/// <summary>
/// What a person's printed statement of a tax form says, beside the amounts.
/// </summary>
public sealed class PayrollTaxStatementTexts
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the line under the title.</summary>
    public string Copy { get; set; }

    /// <summary>Gets or sets the label of the person's tax id.</summary>
    public string TaxIdLabel { get; set; }

    /// <summary>Gets or sets how the person's masked tax id is printed, with {0} its last characters.</summary>
    public string TaxIdFormat { get; set; } = "XXXXX{0}";

    /// <summary>Gets or sets the label of the employer's number.</summary>
    public string EmployerTaxIdLabel { get; set; }

    /// <summary>Gets or sets the employer's number printed.</summary>
    public string EmployerTaxId { get; set; }

    /// <summary>Gets or sets the boxes printed, in order: their label and amount.</summary>
    public IList<(string Label, decimal Amount)> Boxes { get; set; } = [];

    /// <summary>
    /// Gets or sets the column labels of the state and local rows, boxes 15 to 20 in order: the state, the employer's
    /// state id, the state wages, the state income tax, the local wages, the local income tax and the locality name.
    /// </summary>
    public IList<string> StateColumns { get; set; } =
    [
        "15 State",
        "Employer's state ID number",
        "16 State wages, tips, etc.",
        "17 State income tax",
        "18 Local wages, tips, etc.",
        "19 Local income tax",
        "20 Locality name",
    ];
}

/// <summary>
/// Prints a person's statement of a tax form for their records: the employer, the person (the tax id shown by its last
/// characters) and the boxes the form prints, two by row.
/// </summary>
public static class PayrollTaxStatementDocument
{
    private const double Margin = 48;

    /// <summary>
    /// Gets the texts of a person's statement from the form: its printed texts, and the boxes it prints for the person.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="person">The person's statement.</param>
    /// <param name="employer">The employer.</param>
    /// <returns>The texts.</returns>
    public static PayrollTaxStatementTexts TextsFor(PayrollTaxForm form, PayrollFilingPerson person, Employer employer)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(person);

        var statement = form.Statement ?? new PayrollTaxFormStatement();
        var account = string.IsNullOrWhiteSpace(statement.EmployerAccountJurisdiction)
            ? employer?.TaxId
            : employer?.Registrations.FirstOrDefault(registration => string.Equals(registration.Jurisdiction, statement.EmployerAccountJurisdiction, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(registration.AccountId))?.AccountId?.Trim();
        var texts = new PayrollTaxStatementTexts
        {
            Title = string.IsNullOrWhiteSpace(statement.Title) ? form.DisplayName : statement.Title,
            Copy = statement.Copy,
            TaxIdLabel = statement.TaxIdLabel,
            TaxIdFormat = string.IsNullOrWhiteSpace(statement.TaxIdFormat) ? "XXXXX{0}" : statement.TaxIdFormat,
            EmployerTaxIdLabel = statement.EmployerTaxIdLabel,
            EmployerTaxId = account,
        };

        foreach (var box in form.Fields.Where(box => box.Level == PayrollTaxFormFieldLevel.Person))
        {
            var amount = person.Boxes.GetValueOrDefault(box.Number);

            if (box.Print == PayrollTaxFormFieldPrint.Always || (box.Print == PayrollTaxFormFieldPrint.WhenNotZero && amount != 0))
            {
                texts.Boxes.Add((box.Label ?? box.Number, amount));
            }
        }

        return texts;
    }

    /// <summary>
    /// Gets the file name of a person's statement, such as W-2-2026.pdf.
    /// </summary>
    /// <param name="form">The form, or <see langword="null"/> when it is gone.</param>
    /// <param name="filing">The filing.</param>
    /// <returns>The file name.</returns>
    public static string FileName(PayrollTaxForm form, PayrollFiling filing)
    {
        ArgumentNullException.ThrowIfNull(filing);

        var name = !string.IsNullOrWhiteSpace(form?.Statement?.FileName) ? form.Statement.FileName.Trim() : filing.Form;
        var safe = new string([.. name.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-')]);

        return $"{safe}-{filing.Year.ToString(CultureInfo.InvariantCulture)}.pdf";
    }

    /// <summary>
    /// Pairs a statement's state and local amounts into the rows of boxes 15 to 20: each state with its first local
    /// jurisdiction, then a row for each further local jurisdiction of the state (with no state amounts), and a row for
    /// a local jurisdiction of a state with no state amounts.
    /// </summary>
    /// <param name="amounts">The amounts.</param>
    /// <returns>The rows, each a state's amounts (or <see langword="null"/>) and a local jurisdiction's (or <see langword="null"/>).</returns>
    public static IReadOnlyList<(PayrollFilingJurisdictionAmount State, PayrollFilingJurisdictionAmount Local)> StateRows(IEnumerable<PayrollFilingJurisdictionAmount> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        var rows = new List<(PayrollFilingJurisdictionAmount State, PayrollFilingJurisdictionAmount Local)>();

        foreach (var region in amounts.Where(amount => amount is not null).GroupBy(amount => amount.Region, StringComparer.OrdinalIgnoreCase))
        {
            var state = region.FirstOrDefault(amount => !amount.IsLocal);
            var locals = region.Where(amount => amount.IsLocal).ToList();

            if (locals.Count == 0)
            {
                rows.Add((state, null));

                continue;
            }

            for (var index = 0; index < locals.Count; index++)
            {
                rows.Add((index == 0 ? state : null, locals[index]));
            }
        }

        return rows;
    }

    /// <summary>
    /// Writes the PDF.
    /// </summary>
    /// <param name="filing">The filing.</param>
    /// <param name="person">The person's statement.</param>
    /// <param name="employer">The employer.</param>
    /// <param name="employerAddress">The employer's address lines.</param>
    /// <param name="texts">The texts.</param>
    /// <returns>The PDF.</returns>
    public static byte[] Write(PayrollFiling filing, PayrollFilingPerson person, Employer employer, IReadOnlyList<string> employerAddress, PayrollTaxStatementTexts texts)
    {
        ArgumentNullException.ThrowIfNull(filing);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(texts);

        using var writer = new PdfDocumentWriter(texts.Title ?? filing.Form);
        var regular = writer.AddStandardFont();
        var bold = writer.AddStandardFont(bold: true);
        var page = writer.AddPage(PdfPageSize.Letter);
        var right = PdfPageSize.Letter.Width - Margin;
        var y = Margin;

        void Text(string text, PdfFontFace font, double size, double x, double top)
            => page.DrawText(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, x, top, PdfColor.Black);

        void TextRight(string text, PdfFontFace font, double size, double at, double top)
            => page.DrawTextRight(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, at, top, PdfColor.Black);

        Text(texts.Title ?? filing.Form, bold, 14, Margin, y);
        TextRight(filing.Year.ToString(CultureInfo.InvariantCulture), bold, 14, right, y);
        y += 18;

        if (!string.IsNullOrWhiteSpace(texts.Copy))
        {
            Text(texts.Copy, regular, 7, Margin, y);
        }

        y += 20;

        Text(employer?.PrintedName, bold, 10, Margin, y);
        TextRight($"{texts.EmployerTaxIdLabel}: {texts.EmployerTaxId}", regular, 8, right, y);
        y += 13;

        foreach (var line in employerAddress ?? [])
        {
            Text(line, regular, 9, Margin, y);
            y += 12;
        }

        y += 10;
        Text(person.Name, bold, 10, Margin, y);

        var taxId = string.IsNullOrEmpty(person.TaxIdHint) ? "-" : string.Format(CultureInfo.InvariantCulture, texts.TaxIdFormat ?? "{0}", person.TaxIdHint);

        TextRight($"{texts.TaxIdLabel}: {taxId}", regular, 8, right, y);
        y += 13;

        foreach (var line in person.Address)
        {
            Text(line, regular, 9, Margin, y);
            y += 12;
        }

        y += 14;
        page.DrawLine(Margin, y, right, y, PdfColor.Black, 0.5);
        y += 8;

        var half = (right - Margin) / 2;

        for (var i = 0; i < texts.Boxes.Count; i += 2)
        {
            // A form with many boxes goes on to another page.
            if (y + 30 > PdfPageSize.Letter.Height - Margin)
            {
                page = writer.AddPage(PdfPageSize.Letter);
                y = Margin;
            }

            for (var column = 0; column < 2 && i + column < texts.Boxes.Count; column++)
            {
                var (label, amount) = texts.Boxes[i + column];
                var x = Margin + (column * half);

                page.DrawRectangle(x, y, half - 6, 30, PdfColor.Black, 0.5);
                Text(label, regular, 7, x + 4, y + 3);
                TextRight(amount.ToString("N2", CultureInfo.InvariantCulture), bold, 10, x + half - 10, y + 15);
            }

            y += 36;
        }

        // Boxes 15 to 20: a row for each state, with its first local jurisdiction, and a row for each further one.
        var rows = StateRows(person.JurisdictionAmounts ?? []);

        if (rows.Count > 0 && texts.StateColumns is { Count: 7 } columns)
        {
            y += 10;

            var widths = new[] { 0.08, 0.20, 0.14, 0.14, 0.14, 0.14, 0.16 };
            var lefts = new double[widths.Length];
            var width = right - Margin;

            for (var column = 0; column < widths.Length; column++)
            {
                lefts[column] = Margin + (width * widths.Take(column).Sum());
            }

            for (var column = 0; column < columns.Count; column++)
            {
                Text(columns[column], regular, 6, lefts[column] + 2, y);
            }

            y += 10;
            page.DrawLine(Margin, y, right, y, PdfColor.Black, 0.5);
            y += 4;

            foreach (var (state, local) in rows)
            {
                string Amount(decimal? value)
                    => value?.ToString("N2", CultureInfo.InvariantCulture) ?? string.Empty;

                Text(state?.Region?.Split('-').Last() ?? local?.Region?.Split('-').Last(), bold, 9, lefts[0] + 2, y);
                Text(state?.AccountId, regular, 8, lefts[1] + 2, y);
                TextRight(Amount(state?.Wages), regular, 9, lefts[3] - 4, y);
                TextRight(Amount(state?.Tax), regular, 9, lefts[4] - 4, y);
                TextRight(Amount(local?.Wages), regular, 9, lefts[5] - 4, y);
                TextRight(Amount(local?.Tax), regular, 9, lefts[6] - 4, y);
                Text(local?.Name, regular, 8, lefts[6] + 2, y);
                y += 13;
            }
        }

        return writer.Build();
    }
}
