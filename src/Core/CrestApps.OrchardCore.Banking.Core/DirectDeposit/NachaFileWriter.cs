using System.Globalization;
using System.Text;

using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.Core;

namespace CrestApps.OrchardCore.Banking.DirectDeposit;

/// <summary>
/// Writes a bank file in the Nacha format: a file header, one batch of person (PPD) credits and one of business (CCD)
/// credits with their controls, an optional offset debit per batch, the file control, and lines of nines to fill the
/// last block of ten records. Every record is 94 characters.
/// </summary>
public static class NachaFileWriter
{
    /// <summary>
    /// The length of every record.
    /// </summary>
    public const int RecordLength = 94;

    /// <summary>
    /// The longest account number an entry holds: a longer one is refused, never cut.
    /// </summary>
    public const int MaxAccountNumberLength = 17;

    /// <summary>
    /// The number of records in a block.
    /// </summary>
    public const int BlockingFactor = 10;

    /// <summary>
    /// The line break between records.
    /// </summary>
    public const string LineBreak = "\r\n";

    /// <summary>
    /// Writes the file.
    /// </summary>
    /// <param name="request">What the file holds.</param>
    /// <returns>The file and the trace number of each entry.</returns>
    /// <exception cref="ArgumentException">A routing number that is not nine digits, or settings that are missing.</exception>
    public static NachaFileResult Write(NachaFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Settings);

        var settings = request.Settings;

        if (string.IsNullOrWhiteSpace(request.EntryDescription) && string.IsNullOrWhiteSpace(settings.CompanyEntryDescription))
        {
            throw new ArgumentException("The company entry description is required.", nameof(request));
        }

        if (request.Entries.FirstOrDefault(entry => Clean(entry.AccountNumber).Replace(" ", string.Empty, StringComparison.Ordinal).Length > MaxAccountNumberLength) is { } tooLong)
        {
            throw new ArgumentException($"The account number of {tooLong.Key} is longer than {MaxAccountNumberLength} characters.", nameof(request));
        }

        var originRouting = Digits(request.OriginRoutingNumber, 9, "origin routing number");
        var odfi = originRouting[..8];
        var destination = string.IsNullOrWhiteSpace(settings.ImmediateDestination) ? originRouting : Digits(settings.ImmediateDestination, 9, "immediate destination");
        var lines = new List<string>();
        var result = new NachaFileResult();
        var sequence = 0;

        // Trace sequences are seven digits: a series that passes 9999999 starts over at 1.
        string NextTrace()
        {
            var value = sequence < request.TraceSequences.Count ? request.TraceSequences[sequence] : sequence + 1;

            sequence++;

            return odfi + Zero(((value - 1) % 9_999_999) + 1, 7);
        }
        var batchNumber = 0;
        var fileEntryCount = 0;
        long fileHash = 0;
        long fileDebit = 0;
        long fileCredit = 0;

        lines.Add(Record(
            "1",
            "01",
            " " + destination,
            Right(Clean(settings.ImmediateOrigin), 10),
            request.CreatedLocal.ToString("yyMMdd", CultureInfo.InvariantCulture),
            request.CreatedLocal.ToString("HHmm", CultureInfo.InvariantCulture),
            request.FileIdModifier.ToString(),
            "094",
            "10",
            "1",
            Left(Clean(settings.ImmediateDestinationName), 23),
            Left(Clean(settings.ImmediateOriginName), 23),
            Left(string.Empty, 8)));

        foreach (var group in request.Entries.GroupBy(entry => entry.IsBusiness).OrderBy(group => group.Key))
        {
            batchNumber++;

            var entries = group.ToList();
            var offset = settings.IncludeOffset && entries.Any(entry => !entry.IsPrenote && entry.AmountCents > 0);
            var serviceClass = offset ? "200" : "220";
            var sec = group.Key ? "CCD" : "PPD";
            var batchEntryCount = 0;
            long batchHash = 0;
            long batchCredit = 0;
            long batchDebit = 0;

            lines.Add(Record(
                "5",
                serviceClass,
                Left(Clean(settings.CompanyName), 16),
                Left(string.Empty, 20),
                Left(Clean(settings.CompanyIdentification), 10),
                sec,
                Left(Clean(string.IsNullOrWhiteSpace(request.EntryDescription) ? settings.CompanyEntryDescription : request.EntryDescription), 10),
                Left(string.Empty, 6),
                request.EffectiveDate.ToString("yyMMdd", CultureInfo.InvariantCulture),
                Left(string.Empty, 3),
                "1",
                odfi,
                Zero(batchNumber, 7)));

            foreach (var entry in entries)
            {
                var routing = Digits(entry.RoutingNumber, 9, "routing number of " + entry.Key);
                var trace = NextTrace();
                var code = entry.IsSavings ? (entry.IsPrenote ? "33" : "32") : (entry.IsPrenote ? "23" : "22");
                var amount = entry.IsPrenote ? 0 : entry.AmountCents;

                lines.Add(Entry(code, routing, entry.AccountNumber, amount, entry.IndividualId, entry.Name, trace));

                result.TraceNumbers[entry.Key] = trace;
                batchEntryCount++;
                batchHash += long.Parse(routing[..8], CultureInfo.InvariantCulture);
                batchCredit += amount;
            }

            if (offset)
            {
                var account = request.OriginAccountNumber;

                ArgumentException.ThrowIfNullOrEmpty(account, nameof(request.OriginAccountNumber));

                lines.Add(Entry(request.OriginIsSavings ? "37" : "27", originRouting, account, batchCredit, string.Empty, settings.CompanyName, NextTrace()));
                batchEntryCount++;
                batchHash += long.Parse(odfi, CultureInfo.InvariantCulture);
                batchDebit = batchCredit;
            }

            lines.Add(Record(
                "8",
                serviceClass,
                Zero(batchEntryCount, 6),
                Zero(batchHash % 10_000_000_000, 10),
                Zero(batchDebit, 12),
                Zero(batchCredit, 12),
                Left(Clean(settings.CompanyIdentification), 10),
                Left(string.Empty, 19),
                Left(string.Empty, 6),
                odfi,
                Zero(batchNumber, 7)));

            fileEntryCount += batchEntryCount;
            fileHash += batchHash;
            fileDebit += batchDebit;
            fileCredit += batchCredit;
        }

        var recordCount = lines.Count + 1;
        var blockCount = (recordCount + BlockingFactor - 1) / BlockingFactor;

        lines.Add(Record(
            "9",
            Zero(batchNumber, 6),
            Zero(blockCount, 6),
            Zero(fileEntryCount, 8),
            Zero(fileHash % 10_000_000_000, 10),
            Zero(fileDebit, 12),
            Zero(fileCredit, 12),
            Left(string.Empty, 39)));

        while (lines.Count % BlockingFactor != 0)
        {
            lines.Add(new string('9', RecordLength));
        }

        result.Text = string.Join(LineBreak, lines) + LineBreak;
        result.TotalCreditCents = fileCredit;

        return result;
    }

    /// <summary>
    /// Returns the file id modifier of the file made after a number of files the same day: A, then B to Z, then 0 to 9.
    /// </summary>
    /// <param name="filesAlreadyToday">How many files were made that day.</param>
    /// <returns>The modifier.</returns>
    public static char FileIdModifier(int filesAlreadyToday)
    {
        const string modifiers = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        return modifiers[Math.Clamp(filesAlreadyToday, 0, modifiers.Length - 1)];
    }

    /// <summary>
    /// Keeps the characters a bank file may hold, in upper case: letters, digits, spaces and a few marks.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The cleaned text.</returns>
    public static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var upper = char.ToUpperInvariant(character);

            builder.Append(upper is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or ' ' or '-' or '.' or ',' or '&' or '/' or '\'' ? upper : ' ');
        }

        return builder.ToString().Trim();
    }

    private static string Entry(string code, string routing, string account, long amount, string individualId, string name, string trace)
        => Record(
            "6",
            code,
            routing[..8],
            routing[8..],
            Left(Clean(account).Replace(" ", string.Empty, StringComparison.Ordinal), 17),
            Zero(amount, 10),
            Left(Clean(individualId), 15),
            Left(Clean(name), 22),
            "  ",
            "0",
            trace);

    private static string Record(params string[] fields)
    {
        var record = string.Concat(fields);

        if (record.Length != RecordLength)
        {
            throw new InvalidOperationException($"A bank file record is {record.Length} characters instead of {RecordLength}.");
        }

        return record;
    }

    private static string Digits(string value, int length, string what)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        if (digits.Length != length)
        {
            throw new ArgumentException($"The {what} must have {length} digits.");
        }

        return digits;
    }

    private static string Left(string value, int length)
    {
        value ??= string.Empty;

        return value.Length >= length ? value[..length] : value.PadRight(length);
    }

    private static string Right(string value, int length)
    {
        value ??= string.Empty;

        return value.Length >= length ? value[^length..] : value.PadLeft(length);
    }

    private static string Zero(long value, int length)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);

        if (text.Length > length)
        {
            throw new ArgumentException($"The number {value} does not fit in {length} digits.");
        }

        return text.PadLeft(length, '0');
    }
}
