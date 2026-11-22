using System.Globalization;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Pdf;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// Prints checks on letter sheets with <see cref="PdfDocumentWriter"/>: each check at its place on the sheet, its
/// voucher in the other thirds, the signature images and, when the layout asks for it, the check line in the font the
/// site uploaded. Plain text uses a font every PDF reader has, so a server without fonts prints the same checks.
/// </summary>
public sealed class CheckPrinter : ICheckPrinter
{
    private const double VoucherMargin = 36;
    private const double VoucherLineHeight = 12;
    private const string DigitsAndSymbols = "0123456789";

    private readonly IBankAccountManager _bankAccounts;
    private readonly IBankAccountService _bankAccountService;
    private readonly ICheckFileStore _files;
    private readonly IEnumerable<ICheckVoucherSource> _voucherSources;
    private readonly IEnumerable<ICheckMicrFormat> _micrFormats;
    private readonly IEnumerable<IAmountInWords> _amountWriters;
    private readonly IEnumerable<IAddressFormatter> _addressFormatters;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckPrinter"/> class.
    /// </summary>
    /// <param name="bankAccounts">The bank accounts.</param>
    /// <param name="bankAccountService">The bank account rules, which read the account number for the check line.</param>
    /// <param name="files">The files of check layouts.</param>
    /// <param name="voucherSources">The voucher sources of the modules that issue checks.</param>
    /// <param name="micrFormats">The check line formats.</param>
    /// <param name="amountWriters">The writers of amounts in words.</param>
    /// <param name="addressFormatters">The address formatter, when Addresses is on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckPrinter(
        IBankAccountManager bankAccounts,
        IBankAccountService bankAccountService,
        ICheckFileStore files,
        IEnumerable<ICheckVoucherSource> voucherSources,
        IEnumerable<ICheckMicrFormat> micrFormats,
        IEnumerable<IAmountInWords> amountWriters,
        IEnumerable<IAddressFormatter> addressFormatters,
        IStringLocalizer<CheckPrinter> stringLocalizer)
    {
        _bankAccounts = bankAccounts;
        _bankAccountService = bankAccountService;
        _files = files;
        _voucherSources = voucherSources;
        _micrFormats = micrFormats;
        _amountWriters = amountWriters;
        _addressFormatters = addressFormatters;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<CheckPrintResult> PrintAsync(CheckLayout layout, IReadOnlyList<CheckRecord> checks, string printRunId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentException.ThrowIfNullOrEmpty(printRunId);

        var errors = new List<string>();

        if (checks.Count == 0)
        {
            return new CheckPrintResult { Errors = [S["Choose the checks to print."]] };
        }

        foreach (var check in checks.Where(check => check.IsHistorical || check.Status is not (CheckStatus.Issued or CheckStatus.Printed)))
        {
            errors.Add(S["Check {0} is {1} and can't be printed.", check.Number, check.Status.ToString()]);
        }

        var micrFormat = layout.PrintMicr
            ? _micrFormats.FirstOrDefault(format => string.Equals(format.Country, layout.MicrCountry, StringComparison.OrdinalIgnoreCase))
            : null;
        byte[] micrFont = null;

        if (layout.PrintMicr)
        {
            micrFont = layout.HasMicrFont ? await _files.ReadAsync(layout.ItemId, ChecksConstants.LayoutFiles.MicrFont, cancellationToken) : null;

            if (micrFont is null)
            {
                errors.Add(S["The layout prints the check line, but no check line font is uploaded. Upload a TrueType MICR E-13B font on the layout, or turn the check line off for pre-printed stock."]);
            }

            if (micrFormat is null)
            {
                errors.Add(S["No check line format is available for the country {0}.", layout.MicrCountry]);
            }
        }

        if (errors.Count > 0)
        {
            return new CheckPrintResult { Errors = errors };
        }

        var accounts = new Dictionary<string, BankAccount>(StringComparer.Ordinal);

        foreach (var accountId in checks.Select(check => check.BankAccountId).Distinct(StringComparer.Ordinal))
        {
            if (await _bankAccounts.FindByIdAsync(accountId, cancellationToken) is { } account)
            {
                accounts[accountId] = account;
            }
            else
            {
                errors.Add(S["The bank account of a check no longer exists."]);
            }
        }

        if (errors.Count > 0)
        {
            return new CheckPrintResult { Errors = errors };
        }

        // Every check line is laid out before anything is drawn, so a number the line can't hold stops the run with
        // the bank account's name, and each account number is read once for the run.
        var micrLines = new Dictionary<CheckRecord, MicrLine>(ReferenceEqualityComparer.Instance);

        if (layout.PrintMicr)
        {
            foreach (var group in checks.GroupBy(check => check.BankAccountId, StringComparer.Ordinal))
            {
                var account = accounts[group.Key];

                if (!BankAccountNumbers.HasNumbers(account))
                {
                    errors.Add(S["{0} has no routing or account number, which the check line needs. Enter them on the bank account, or print on pre-printed stock without the check line.", account.Name]);

                    continue;
                }

                var numbers = group.Select(check => check.Number).ToList();
                var accountNumber = await _bankAccountService.RevealAccountNumberAsync(
                    account,
                    S["Printed checks {0} to {1} in print run {2}", numbers.Min(), numbers.Max(), printRunId],
                    cancellationToken);

                if (string.IsNullOrEmpty(accountNumber))
                {
                    errors.Add(S["The account number of {0} can't be read on this site. Enter it again on the bank account.", account.Name]);

                    continue;
                }

                try
                {
                    foreach (var check in group)
                    {
                        micrLines[check] = micrFormat.Format(new MicrLineRequest
                        {
                            RoutingNumber = account.RoutingNumber,
                            AccountNumber = accountNumber,
                            CheckNumber = check.Number,
                            UseAuxiliaryOnUs = layout.UseAuxiliaryOnUs,
                        });
                    }
                }
                catch (ArgumentException)
                {
                    errors.Add(S["The routing or account number of {0} doesn't fit the check line. Check the numbers on the bank account.", account.Name]);
                }
            }
        }

        if (errors.Count > 0)
        {
            return new CheckPrintResult { Errors = errors };
        }

        var signature = layout.HasSignature ? await _files.ReadAsync(layout.ItemId, ChecksConstants.LayoutFiles.Signature, cancellationToken) : null;
        var secondSignature = layout.HasSecondSignature ? await _files.ReadAsync(layout.ItemId, ChecksConstants.LayoutFiles.SecondSignature, cancellationToken) : null;
        var amountWriter = _amountWriters.FirstOrDefault(writer => string.Equals(writer.Language, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            ?? _amountWriters.FirstOrDefault(writer => writer.Language == "en");

        using var writer = new PdfDocumentWriter(S["Checks"]);
        var regular = writer.AddStandardFont();
        var bold = writer.AddStandardFont(bold: true);
        PdfFontFace micr = null;

        if (micrFont is not null)
        {
            micr = writer.AddSymbolFont(micrFont, DigitsAndSymbols + layout.MicrSymbolCharacters);

            if (micr is null)
            {
                return new CheckPrintResult { Errors = [S["The check line font can't draw the digits 0 to 9 and the symbol characters {0}. Upload a TrueType MICR E-13B font, and enter the characters it draws the four symbols on.", layout.MicrSymbolCharacters]] };
            }
        }

        var (checkTops, voucherTops) = CheckLayoutDefaults.GetAreas(layout.Form);
        var perPage = checkTops.Length;
        PdfCanvas page = null;

        for (var index = 0; index < checks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var check = checks[index];
            var slot = index % perPage;

            if (slot == 0)
            {
                page = writer.AddPage(PdfPageSize.Letter);
            }

            var top = checkTops[slot];
            var account = accounts[check.BankAccountId];
            var context = new CheckDrawing(page, layout, top, regular, bold);

            await DrawCheckAsync(context, check, account, amountWriter, signature, secondSignature, cancellationToken);

            if (micr is not null)
            {
                DrawMicr(context, micrLines[check], micr);
            }

            foreach (var voucherTop in voucherTops)
            {
                await DrawVoucherAsync(page, layout, voucherTop, check, regular, bold, cancellationToken);
            }
        }

        return new CheckPrintResult { Pdf = writer.Build() };
    }

    private async Task DrawCheckAsync(CheckDrawing context, CheckRecord check, BankAccount account, IAmountInWords amountWriter, byte[] signature, byte[] secondSignature, CancellationToken cancellationToken)
    {
        var layout = context.Layout;

        if (layout.Stock == CheckStock.Blank)
        {
            var payer = new List<string>();

            if (!string.IsNullOrWhiteSpace(layout.PayerName))
            {
                payer.Add(layout.PayerName);
            }

            payer.AddRange(await AddressLinesAsync(layout.PayerAddress));
            context.Lines(CheckFieldNames.Payer, payer, boldFirst: true);
            context.Lines(CheckFieldNames.Bank, [account.BankName ?? string.Empty]);
        }

        context.Text(CheckFieldNames.Number, check.Number.ToString(CultureInfo.InvariantCulture), bold: true);
        context.Text(CheckFieldNames.Date, check.CheckDate.ToString("d", CultureInfo.CurrentCulture));
        context.Text(CheckFieldNames.Payee, check.PayeeName, bold: true);
        context.Text(CheckFieldNames.Amount, "**" + FormatAmount(check));

        if (amountWriter is not null)
        {
            context.Text(CheckFieldNames.AmountInWords, amountWriter.Write(check.Amount, CurrencyScale.GetDecimalPlaces(check.Currency), check.Currency) + " ***");
        }

        context.Lines(CheckFieldNames.PayeeAddress, [check.PayeeName, .. await AddressLinesAsync(check.PayeeAddress)]);

        if (!string.IsNullOrEmpty(check.Memo))
        {
            context.Text(CheckFieldNames.Memo, S["Memo: {0}", check.Memo]);
        }

        context.SignatureLine(CheckFieldNames.Signature, signature, S["Authorized signature"]);

        if (layout.SecondSignatureAbove is decimal threshold && check.Amount > threshold)
        {
            context.SignatureLine(CheckFieldNames.SecondSignature, secondSignature, S["Second signature"]);
        }
    }

    private static void DrawMicr(CheckDrawing context, MicrLine line, PdfFontFace font)
    {
        var layout = context.Layout;
        var baseline = context.Top + CheckLayoutDefaults.CheckHeight - CheckLayoutDefaults.MicrBaselineFromBottom + layout.OffsetY;

        foreach (var character in line.Characters)
        {
            var glyph = MicrSymbols.ToFont(character.Character.ToString(), layout.MicrSymbolCharacters);
            var x = MicrLine.LeftOf(character.Position, CheckLayoutDefaults.CheckWidth) + layout.OffsetX;

            context.Page.DrawTextOnBaseline(glyph, font, layout.MicrFontSize, x, baseline, PdfColor.Black);
        }
    }

    private async Task DrawVoucherAsync(PdfCanvas page, CheckLayout layout, double top, CheckRecord check, PdfFontFace regular, PdfFontFace bold, CancellationToken cancellationToken)
    {
        var source = string.IsNullOrEmpty(check.SourceKind)
            ? null
            : _voucherSources.FirstOrDefault(entry => string.Equals(entry.SourceKind, check.SourceKind, StringComparison.Ordinal));
        var voucher = source is null ? null : await source.GetVoucherAsync(check, cancellationToken);

        voucher ??= new CheckVoucher
        {
            Title = S["Check {0}", check.Number],
            Columns = [S["Paid to"], S["Amount"]],
            Lines =
            [
                new CheckVoucherLine { Label = check.PayeeName, Values = [FormatAmount(check)] },
                new CheckVoucherLine { Label = check.Memo ?? string.Empty, Values = [] },
            ],
        };

        var x = VoucherMargin + layout.OffsetX;
        var y = top + VoucherMargin / 2 + layout.OffsetY;
        var width = CheckLayoutDefaults.CheckWidth - (VoucherMargin * 2);
        var valueColumns = Math.Max(1, voucher.Columns.Count - 1);
        var labelWidth = width * 0.46;
        var valueWidth = (width - labelWidth) / valueColumns;
        var bottom = top + CheckLayoutDefaults.CheckHeight - (VoucherMargin / 2);

        page.DrawText(regular.Prepare(PdfText.Fit(PdfText.Clean(voucher.Title ?? string.Empty), bold, 10, width)), bold, 10, x, y, PdfColor.Black);
        page.DrawText(regular.Prepare(S["{0}, {1}, check {2}, {3}", check.PayeeName, check.CheckDate.ToString("d", CultureInfo.CurrentCulture), check.Number, FormatAmount(check)]), regular, 8, x, y + VoucherLineHeight, PdfColor.Black);
        y += VoucherLineHeight * 2.5;

        if (voucher.Columns.Count > 0)
        {
            DrawRow(page, bold, x, y, labelWidth, valueWidth, voucher.Columns[0], voucher.Columns.Skip(1).ToArray());
            page.DrawLine(x, y + VoucherLineHeight, x + width, y + VoucherLineHeight, PdfColor.Black, 0.5);
            y += VoucherLineHeight + 2;
        }

        foreach (var line in voucher.Lines)
        {
            if (y + VoucherLineHeight > bottom)
            {
                page.DrawText(regular.Prepare(S["More lines than the voucher holds; see the statement."]), regular, 7, x, y, PdfColor.Black);

                break;
            }

            DrawRow(page, line.IsEmphasized ? bold : regular, x, y, labelWidth, valueWidth, line.Label, line.Values);
            y += VoucherLineHeight;
        }
    }

    private static void DrawRow(PdfCanvas page, PdfFontFace font, double x, double y, double labelWidth, double valueWidth, string label, IReadOnlyList<string> values)
    {
        page.DrawText(font.Prepare(PdfText.Fit(PdfText.Clean(label ?? string.Empty), font, 8, labelWidth - 4)), font, 8, x, y, PdfColor.Black);

        for (var i = 0; i < values.Count; i++)
        {
            var text = font.Prepare(PdfText.Fit(PdfText.Clean(values[i] ?? string.Empty), font, 8, valueWidth - 4));

            page.DrawTextRight(text, font, 8, x + labelWidth + (valueWidth * (i + 1)), y, PdfColor.Black);
        }
    }

    private static string FormatAmount(CheckRecord check)
        => CurrencyScale.Round(check.Amount, check.Currency).ToString("N" + CurrencyScale.GetDecimalPlaces(check.Currency), CultureInfo.CurrentCulture);

    private async Task<IReadOnlyList<string>> AddressLinesAsync(Address address)
    {
        if (address is null)
        {
            return [];
        }

        if (_addressFormatters.FirstOrDefault() is { } formatter)
        {
            return await formatter.GetLinesAsync(address, new AddressFormatOptions { IncludeCountry = false, Envelope = true });
        }

        var cityLine = string.Join(" ", new[] { address.City, address.Region, address.PostalCode }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new[] { address.AddressLine1, address.AddressLine2, cityLine }.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
    }

    // The place of a field on one check: the layout's place, moved by the calibration and the check's top on the sheet.
    private sealed class CheckDrawing
    {
        public CheckDrawing(PdfCanvas page, CheckLayout layout, double top, PdfFontFace regular, PdfFontFace bold)
        {
            Page = page;
            Layout = layout;
            Top = top;
            Regular = regular;
            Bold = bold;
        }

        public PdfCanvas Page { get; }

        public CheckLayout Layout { get; }

        public double Top { get; }

        public PdfFontFace Regular { get; }

        public PdfFontFace Bold { get; }

        public void Text(string field, string text, bool bold = false)
        {
            var position = CheckLayoutDefaults.Get(Layout, field);

            if (position.Hidden || string.IsNullOrEmpty(text))
            {
                return;
            }

            var font = bold ? Bold : Regular;
            var prepared = font.Prepare(PdfText.Clean(text));

            if (position.Width > 0)
            {
                prepared = PdfText.Fit(prepared, font, position.FontSize, position.Width);
            }

            Page.DrawText(prepared, font, position.FontSize, X(position), Y(position), PdfColor.Black);
        }

        public void Lines(string field, IEnumerable<string> lines, bool boldFirst = false)
        {
            var position = CheckLayoutDefaults.Get(Layout, field);

            if (position.Hidden)
            {
                return;
            }

            var offset = 0d;
            var first = true;

            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                var font = first && boldFirst ? Bold : Regular;
                var prepared = font.Prepare(PdfText.Clean(line));

                if (position.Width > 0)
                {
                    prepared = PdfText.Fit(prepared, font, position.FontSize, position.Width);
                }

                Page.DrawText(prepared, font, position.FontSize, X(position), Y(position) + offset, PdfColor.Black);
                offset += position.FontSize * 1.25;
                first = false;
            }
        }

        public void SignatureLine(string field, byte[] image, string caption)
        {
            var position = CheckLayoutDefaults.Get(Layout, field);

            if (position.Hidden)
            {
                return;
            }

            var width = position.Width > 0 ? position.Width : 216;
            var lineY = Y(position) + 30;

            if (image is not null)
            {
                Page.DrawImage(image, X(position), Y(position), width, 28);
            }

            Page.DrawLine(X(position), lineY, X(position) + width, lineY, PdfColor.Black, 0.5);
            Page.DrawText(Regular.Prepare(caption), Regular, position.FontSize, X(position), lineY + 2, PdfColor.Black);
        }

        private double X(CheckFieldPosition position)
            => position.X + Layout.OffsetX;

        private double Y(CheckFieldPosition position)
            => Top + position.Y + Layout.OffsetY;
    }
}
