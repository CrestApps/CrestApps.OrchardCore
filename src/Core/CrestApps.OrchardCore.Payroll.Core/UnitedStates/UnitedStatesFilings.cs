using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Core.UnitedStates;

/// <summary>
/// The tax codes of the federal rules the United States recipes ship.
/// </summary>
public static class UnitedStatesTaxCodes
{
    /// <summary>Federal income tax.</summary>
    public const string IncomeTax = "US-FIT";

    /// <summary>Social Security, employee.</summary>
    public const string SocialSecurity = "US-SS-EE";

    /// <summary>Social Security, employer.</summary>
    public const string SocialSecurityEmployer = "US-SS-ER";

    /// <summary>Medicare, employee.</summary>
    public const string Medicare = "US-MED-EE";

    /// <summary>Medicare, employer.</summary>
    public const string MedicareEmployer = "US-MED-ER";

    /// <summary>Additional Medicare, employee.</summary>
    public const string AdditionalMedicare = "US-MED-ADD";

    /// <summary>Federal unemployment.</summary>
    public const string Unemployment = "US-FUTA";
}

/// <summary>
/// The stored names of the United States tax forms the United States recipe adds, which the EFW2 file and the data sets
/// read.
/// </summary>
public static class UnitedStatesTaxForms
{
    /// <summary>Form 941, the employer's quarterly federal tax return.</summary>
    public const string Form941 = "US-941";

    /// <summary>Form 940, the employer's annual federal unemployment return.</summary>
    public const string Form940 = "US-940";

    /// <summary>Form 945, the annual return of withheld federal income tax.</summary>
    public const string Form945 = "US-945";

    /// <summary>Forms W-2 and W-3, the wage and tax statements.</summary>
    public const string FormW2 = "US-W2";

    /// <summary>Forms 1099-NEC and 1096, nonemployee compensation.</summary>
    public const string Form1099Nec = "US-1099NEC";

    /// <summary>The prefix of the form box names pay codes report W-2 boxes to, such as W2:12D.</summary>
    public const string FormW2PayCodeFieldPrefix = "W2:";

    /// <summary>The tax code of backup withholding on contractors' payments.</summary>
    public const string BackupWithholdingCode = "US-BWH";
}

/// <summary>
/// Writes the Social Security Administration's EFW2 file of wage statements: a submitter record (RA), then for each
/// employer its record (RE), a record per person (RW) and a total record (RT), and a final record (RF); every record
/// 512 characters, amounts in cents right-aligned with zeros, text upper-cased and left-aligned with spaces. Fields
/// this writer does not fill are left as the specification's blanks or zero amounts.
/// </summary>
public static class UnitedStatesEfw2
{
    /// <summary>The length of every record.</summary>
    public const int RecordLength = 512;

    /// <summary>The money fields of an RW record (box: start, length 11) and of the RT record (start, length 15).</summary>
    public static readonly IReadOnlyList<(string Box, int Employee, int Total)> MoneyFields =
    [
        ("1", 188, 10),
        ("2", 199, 25),
        ("3", 210, 40),
        ("4", 221, 55),
        ("5", 232, 70),
        ("6", 243, 85),
        ("7", 254, 100),
        ("10", 276, 130),
        ("12D", 287, 145),
        ("12E", 298, 160),
        ("12F", 309, 175),
        ("12G", 320, 190),
        ("12H", 331, 205),
    ];

    /// <summary>The box 12 codes of retirement plan contributions, any of which sets the retirement plan indicator.</summary>
    public static readonly IReadOnlyList<string> RetirementPlanCodes = ["12D", "12E", "12F", "12G", "12H", "12S"];

    /// <summary>
    /// Gets the boxes of a person with an amount the file doesn't write: box 12 codes other than D to H, and boxes 8
    /// and 11. A file with one of them is refused, rather than written without it.
    /// </summary>
    /// <param name="boxes">The person's boxes.</param>
    /// <returns>The boxes and their amounts, such as 12W 1200.00, or an empty text.</returns>
    public static string Unwritten(IReadOnlyDictionary<string, decimal> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        return string.Join(", ", boxes
            .Where(pair => pair.Value != 0 && (pair.Key is "8" or "11" || (pair.Key.StartsWith("12", StringComparison.Ordinal) && pair.Key.Length > 2 && !MoneyFields.Any(field => field.Box == pair.Key))))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key} {pair.Value.ToString("0.00", CultureInfo.InvariantCulture)}"));
    }

    /// <summary>
    /// Writes the file.
    /// </summary>
    /// <param name="submitter">The submitter (the first employer).</param>
    /// <param name="employers">The employers, each with its people.</param>
    /// <param name="year">The tax year.</param>
    /// <returns>The file's text, records ending with a carriage return and a line feed.</returns>
    public static string Write(Efw2Submitter submitter, IReadOnlyList<Efw2Employer> employers, int year)
    {
        ArgumentNullException.ThrowIfNull(submitter);
        ArgumentNullException.ThrowIfNull(employers);

        var file = new StringBuilder();
        var people = 0;

        file.Append(Submitter(submitter)).Append("\r\n");

        foreach (var employer in employers)
        {
            file.Append(Employer(employer, year)).Append("\r\n");

            foreach (var person in employer.People)
            {
                file.Append(Employee(person)).Append("\r\n");
            }

            file.Append(Total(employer.People)).Append("\r\n");
            people += employer.People.Count;
        }

        var final = Blank("RF");

        Set(final, 8, 9, people.ToString(CultureInfo.InvariantCulture), numeric: true);
        file.Append(final).Append("\r\n");

        return file.ToString();
    }

    private static string Submitter(Efw2Submitter submitter)
    {
        var record = Blank("RA");

        Set(record, 3, 9, Digits(submitter.Ein), numeric: true);
        Set(record, 12, 8, submitter.UserId);
        Set(record, 29, 1, "0");
        Set(record, 36, 2, "98");
        Set(record, 38, 57, submitter.Name);
        Set(record, 95, 22, submitter.Address.Location);
        Set(record, 117, 22, submitter.Address.Delivery);
        Set(record, 139, 22, submitter.Address.City);
        Set(record, 161, 2, submitter.Address.State);
        Set(record, 163, 5, submitter.Address.Zip);
        Set(record, 168, 4, submitter.Address.ZipExtension);
        Set(record, 217, 57, submitter.Name);
        Set(record, 274, 22, submitter.Address.Location);
        Set(record, 296, 22, submitter.Address.Delivery);
        Set(record, 318, 22, submitter.Address.City);
        Set(record, 340, 2, submitter.Address.State);
        Set(record, 342, 5, submitter.Address.Zip);
        Set(record, 347, 4, submitter.Address.ZipExtension);
        Set(record, 396, 27, submitter.ContactName);
        Set(record, 423, 15, Digits(submitter.ContactPhone));
        Set(record, 446, 40, submitter.ContactEmail, upper: false);

        // The preparer code: the employer prepared its own file.
        Set(record, 500, 1, "L");

        return new string(record);
    }

    private static string Employer(Efw2Employer employer, int year)
    {
        var record = Blank("RE");

        Set(record, 3, 4, year.ToString(CultureInfo.InvariantCulture), numeric: true);
        Set(record, 8, 9, Digits(employer.Ein), numeric: true);
        Set(record, 26, 1, "0");
        Set(record, 40, 57, employer.Name);
        Set(record, 97, 22, employer.Address.Location);
        Set(record, 119, 22, employer.Address.Delivery);
        Set(record, 141, 22, employer.Address.City);
        Set(record, 163, 2, employer.Address.State);
        Set(record, 165, 5, employer.Address.Zip);
        Set(record, 170, 4, employer.Address.ZipExtension);
        Set(record, 174, 1, "N");
        Set(record, 219, 1, "R");
        Set(record, 221, 1, "0");

        return new string(record);
    }

    private static string Employee(Efw2Person person)
    {
        var record = Blank("RW");

        Set(record, 3, 9, Digits(person.Ssn), numeric: true);
        Set(record, 12, 15, person.FirstName);
        Set(record, 27, 15, person.MiddleName);
        Set(record, 42, 20, person.LastName);
        Set(record, 66, 22, person.Address.Location);
        Set(record, 88, 22, person.Address.Delivery);
        Set(record, 110, 22, person.Address.City);
        Set(record, 132, 2, person.Address.State);
        Set(record, 134, 5, person.Address.Zip);
        Set(record, 139, 4, person.Address.ZipExtension);

        foreach (var (box, start, _) in MoneyFields)
        {
            Set(record, start, 11, Cents(person.Boxes.GetValueOrDefault(box)), numeric: true);
        }

        Set(record, 488, 1, "0");
        Set(record, 490, 1, RetirementPlanCodes.Any(code => person.Boxes.GetValueOrDefault(code) > 0) ? "1" : "0");
        Set(record, 491, 1, "0");

        return new string(record);
    }

    private static string Total(IReadOnlyList<Efw2Person> people)
    {
        var record = Blank("RT");

        Set(record, 3, 7, people.Count.ToString(CultureInfo.InvariantCulture), numeric: true);

        foreach (var (box, _, start) in MoneyFields)
        {
            Set(record, start, 15, Cents(people.Sum(person => person.Boxes.GetValueOrDefault(box))), numeric: true);
        }

        return new string(record);
    }

    private static char[] Blank(string identifier)
    {
        var record = new char[RecordLength];

        Array.Fill(record, ' ');
        record[0] = identifier[0];
        record[1] = identifier[1];

        return record;
    }

    // Puts a value at a 1-based position: text left-aligned and cut, numbers right-aligned with zeros.
    private static void Set(char[] record, int start, int length, string value, bool numeric = false, bool upper = true)
    {
        var text = value ?? string.Empty;

        text = numeric ? text.PadLeft(length, '0')[^length..] : (upper ? Clean(text).ToUpperInvariant() : text);

        for (var i = 0; i < length; i++)
        {
            record[start - 1 + i] = i < text.Length ? text[i] : (numeric ? '0' : ' ');
        }
    }

    private static string Cents(decimal amount)
        => ((long)Math.Round(Math.Max(0, amount) * 100, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private static string Digits(string value)
        => new([.. (value ?? string.Empty).Where(char.IsAsciiDigit)]);

    private static string Clean(string value)
        => new([.. value.Normalize(NormalizationForm.FormD).Where(character => character is >= ' ' and <= '~')]);
}

/// <summary>An address as the EFW2 file wants it.</summary>
public sealed record Efw2Address(string Location, string Delivery, string City, string State, string Zip, string ZipExtension)
{
    /// <summary>An empty address.</summary>
    public static Efw2Address Empty { get; } = new(null, null, null, null, null, null);
}

/// <summary>The submitter of an EFW2 file.</summary>
public sealed record Efw2Submitter(string Ein, string UserId, string Name, Efw2Address Address, string ContactName, string ContactPhone, string ContactEmail);

/// <summary>An employer of an EFW2 file.</summary>
public sealed record Efw2Employer(string Ein, string Name, Efw2Address Address, IReadOnlyList<Efw2Person> People);

/// <summary>A person of an EFW2 file.</summary>
public sealed record Efw2Person(string Ssn, string FirstName, string MiddleName, string LastName, Efw2Address Address, IReadOnlyDictionary<string, decimal> Boxes);
