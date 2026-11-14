using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Disbursements.Core.Nacha;

using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// The bank file (Nacha) checked record by record: lengths, blocking, hashes, totals and trace numbers; and the
/// return and change file read back.
/// </summary>
public sealed class NachaFileTests
{
    private static readonly NachaOriginSettings _settings = new()
    {
        Enabled = true,
        ImmediateDestinationName = "First Bank",
        ImmediateOrigin = "1123456789",
        ImmediateOriginName = "Contoso Ltd",
        CompanyName = "Contoso Ltd",
        CompanyIdentification = "1123456789",
        CompanyEntryDescription = "PAYROLL",
    };

    [Fact]
    public void Write_EveryRecordIs94Characters_InBlocksOfTen()
    {
        // Act
        var file = NachaFileWriter.Write(Request(Person("a", "021000021", 125050), Person("b", "011000015", 50000)));
        var lines = Lines(file);

        // Assert
        Assert.All(lines, line => Assert.Equal(94, line.Length));
        Assert.Equal(0, lines.Count % 10);
        Assert.Equal('1', lines[0][0]);
        Assert.Equal(new string('9', 94), lines[^1]);
    }

    [Fact]
    public void Write_TheFileHeader_HoldsTheDestinationOriginDateAndModifier()
    {
        // Act
        var header = Lines(NachaFileWriter.Write(Request(Person("a", "021000021", 100))))[0];

        // Assert
        Assert.Equal("101", header[..3]);
        Assert.Equal(" 900000001", header[3..13]);
        Assert.Equal("1123456789", header[13..23]);
        Assert.Equal("261113", header[23..29]);
        Assert.Equal("1530", header[29..33]);
        Assert.Equal('B', header[33]);
        Assert.Equal("094101", header[34..40]);
        Assert.Equal("FIRST BANK".PadRight(23), header[40..63]);
        Assert.Equal("CONTOSO LTD".PadRight(23), header[63..86]);
    }

    [Fact]
    public void Write_AnEntry_HoldsTheCodeRoutingAccountAmountNameAndTrace()
    {
        // Act
        var file = NachaFileWriter.Write(Request(Person("a", "021000021", 125050, name: "Zoë Ann-Marie O'Neil")));
        var entry = Lines(file).Single(line => line[0] == '6');

        // Assert
        Assert.Equal("22", entry[1..3]);
        Assert.Equal("02100002", entry[3..11]);
        Assert.Equal('1', entry[11]);
        Assert.Equal("123456789".PadRight(17), entry[12..29]);
        Assert.Equal("0000125050", entry[29..39]);
        Assert.Equal("ZOE ANN-MARIE O'NEIL".PadRight(22), entry[54..76]);
        Assert.Equal('0', entry[78]);
        Assert.Equal("900000000000001", entry[79..94]);
        Assert.Equal("900000000000001", file.TraceNumbers["a"]);
    }

    [Fact]
    public void Write_TheBatchAndFileControls_CountHashAndTotalTheEntries()
    {
        // Act
        var file = NachaFileWriter.Write(Request(Person("a", "021000021", 125050), Person("b", "011000015", 50000)));
        var lines = Lines(file);
        var batchControl = lines.Single(line => line[0] == '8');
        var fileControl = lines.Single(line => line[0] == '9' && line != new string('9', 94));

        // Assert: 02100002 + 01100001 = 03200003.
        Assert.Equal("220", batchControl[1..4]);
        Assert.Equal("000002", batchControl[4..10]);
        Assert.Equal("0003200003", batchControl[10..20]);
        Assert.Equal("000000000000", batchControl[20..32]);
        Assert.Equal("000000175050", batchControl[32..44]);
        Assert.Equal("000001", fileControl[1..7]);
        Assert.Equal("000001", fileControl[7..13]);
        Assert.Equal("00000002", fileControl[13..21]);
        Assert.Equal("0003200003", fileControl[21..31]);
        Assert.Equal("000000175050", fileControl[43..55]);
        Assert.Equal(175050, file.TotalCreditCents);
    }

    [Fact]
    public void Write_PeopleAndBusinesses_GoInSeparatePpdAndCcdBatches()
    {
        // Act
        var file = NachaFileWriter.Write(Request(Person("a", "021000021", 100), Person("b", "011000015", 200, business: true)));
        var headers = Lines(file).Where(line => line[0] == '5').ToList();

        // Assert
        Assert.Equal(["PPD", "CCD"], headers.Select(header => header[50..53]));
        Assert.Equal(["0000001", "0000002"], headers.Select(header => header[87..94]));
        Assert.Equal("PAYROLL   ", headers[0][53..63]);
        Assert.Equal("261116", headers[0][69..75]);
        Assert.Equal("90000000", headers[0][79..87]);
    }

    [Fact]
    public void Write_APrenote_CarriesNoAmount_WithTheTestCode()
    {
        // Act
        var file = NachaFileWriter.Write(Request(Person("a", "021000021", 999, prenote: true), Person("s", "011000015", 0, prenote: true, savings: true)));
        var entries = Lines(file).Where(line => line[0] == '6').ToList();

        // Assert
        Assert.Equal(["23", "33"], entries.Select(entry => entry[1..3]));
        Assert.All(entries, entry => Assert.Equal("0000000000", entry[29..39]));
    }

    [Fact]
    public void Write_WithAnOffset_DebitsTheBusinessAccountForTheTotal()
    {
        // Arrange
        var request = Request(Person("a", "021000021", 125050));
        request.Settings = new NachaOriginSettings { ImmediateOrigin = "1123456789", CompanyName = "Contoso", CompanyIdentification = "1123456789", CompanyEntryDescription = "PAYROLL", IncludeOffset = true };
        request.OriginAccountNumber = "555000111";

        // Act
        var lines = Lines(NachaFileWriter.Write(request));
        var offset = lines.Where(line => line[0] == '6').Last();
        var control = lines.Single(line => line[0] == '8');

        // Assert
        Assert.Equal("27", offset[1..3]);
        Assert.Equal("555000111".PadRight(17), offset[12..29]);
        Assert.Equal("0000125050", offset[29..39]);
        Assert.Equal("200", control[1..4]);
        Assert.Equal("000002", control[4..10]);
        Assert.Equal("000000125050", control[20..32]);
        Assert.Equal("000000125050", control[32..44]);
    }

    [Fact]
    public void Write_ARoutingNumberThatIsNotNineDigits_IsRefused()
        => Assert.Throws<ArgumentException>(() => NachaFileWriter.Write(Request(Person("a", "12345", 100))));

    [Theory]
    [InlineData(0, 'A')]
    [InlineData(1, 'B')]
    [InlineData(25, 'Z')]
    [InlineData(26, '0')]
    [InlineData(99, '9')]
    public void FileIdModifier_LettersTheFilesOfADay(int filesAlreadyToday, char expected)
        => Assert.Equal(expected, NachaFileWriter.FileIdModifier(filesAlreadyToday));

    [Fact]
    public void ReadReturnFile_ReadsReturnsAndNoticesOfChange()
    {
        // Arrange: a return of trace 900000000000001 (R03) and a notice of change C01 for 900000000000002.
        var text = string.Join("\r\n",
            Pad("101 900000001 112345678926111315300A094101FIRST BANK"),
            Pad("621021000021123456789        00001250500000000000     PAT DOE                1900000000000001"),
            Pad("799R03900000000000001      02100002                                            021000020000001"),
            Pad("621021000021555           00000000000000000000     LEE DOE                1900000000000002"),
            Pad("798C01900000000000002      02100002987654321                                  021000020000002"));

        // Act
        var (returns, notices, problems) = NachaReturnFileReader.Read(text);

        // Assert
        Assert.Empty(problems);
        var returned = Assert.Single(returns);
        Assert.Equal("900000000000001", returned.TraceNumber);
        Assert.Equal("R03", returned.Code);
        Assert.Null(returned.Description);
        Assert.Contains("can't be found", NachaReasonCodes.Describe(returned.Code, RecipeRoundTrip.Localizer<NachaFileTests>()).Value, StringComparison.Ordinal);
        var notice = Assert.Single(notices);
        Assert.Equal("C01", notice.Code);
        Assert.Equal("900000000000002", notice.TraceNumber);
        Assert.Equal("987654321", NachaChangeCorrection.Parse(notice.Code, notice.CorrectedData).AccountNumber);
    }

    [Theory]
    [InlineData("C01", "987654321", null, "987654321", null)]
    [InlineData("C02", "011000015", "011000015", null, null)]
    [InlineData("C03", "011000015   987654321", "011000015", "987654321", null)]
    [InlineData("C05", "32", null, null, BankAccountKind.Savings)]
    [InlineData("C06", "987654321           32", null, "987654321", BankAccountKind.Savings)]
    [InlineData("C07", "011000015987654321        22", "011000015", "987654321", BankAccountKind.Checking)]
    public void ChangeCorrection_ReadsEachKindOfNotice(string code, string data, string routing, string account, BankAccountKind? kind)
    {
        // Act
        var correction = NachaChangeCorrection.Parse(code, data);

        // Assert
        Assert.Equal(routing, correction.RoutingNumber);
        Assert.Equal(account, correction.AccountNumber);
        Assert.Equal(kind, correction.Kind);
    }

    [Fact]
    public void ChangeCorrection_AnUnknownCode_IsNotApplied()
        => Assert.Null(NachaChangeCorrection.Parse("C09", "anything"));

    internal static NachaFileRequest Request(params NachaEntry[] entries)
        => new()
        {
            Settings = _settings,
            OriginRoutingNumber = "900000001",
            CreatedLocal = new DateTime(2026, 11, 13, 15, 30, 0),
            FileIdModifier = 'B',
            EffectiveDate = new DateOnly(2026, 11, 16),
            Entries = entries,
        };

    internal static NachaEntry Person(string key, string routing, long cents, bool business = false, bool prenote = false, bool savings = false, string name = "Pat Doe")
        => new()
        {
            Key = key,
            RoutingNumber = routing,
            AccountNumber = "123456789",
            AmountCents = cents,
            IsBusiness = business,
            IsPrenote = prenote,
            IsSavings = savings,
            IndividualId = "staff-" + key,
            Name = name,
        };

    // A bank return file with one return of the entry with the given trace number.
    internal static string ReturnFile(string traceNumber, string code)
        => string.Join("\r\n",
            Pad("101 900000001 112345678926111315300A094101FIRST BANK"),
            Pad("621021000021123456789        00001250500000000000     PAT DOE                1" + traceNumber),
            Pad("799" + code + traceNumber + "      02100002                                            021000020000001"));

    private static List<string> Lines(NachaFileResult file)
        => file.Text.Split(NachaFileWriter.LineBreak, StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string Pad(string record)
        => record.PadRight(94);
}
