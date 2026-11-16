using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;

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
/// Sums over statements the United States forms share.
/// </summary>
public static class UnitedStatesFilingSums
{
    /// <summary>Gets the wages of a tax category of statements, from their pay code lines.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="category">The tax category.</param>
    /// <returns>The wages.</returns>
    public static decimal Wages(IEnumerable<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes, string category)
        => statements.Sum(statement =>
        {
            // A reversal's lines are the opposite of the statement it voids: worked out as that statement, then taken off.
            var sign = statement.VoidOf is null ? 1 : -1;

            return sign * PayCodeTaxMatrix.WagesFor(category, statement.Lines
                .Where(line => line.Type != PayLineType.Tax && line.PayCodeId is not null && codes.ContainsKey(line.PayCodeId))
                .Select(line => new PayCodeAmount(codes[line.PayCodeId], sign * line.Amount)));
        });

    /// <summary>Gets the amount of a tax.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="taxCodes">The tax codes.</param>
    /// <returns>The amount.</returns>
    public static decimal Tax(IEnumerable<PayStatement> statements, params string[] taxCodes)
        => statements.SelectMany(statement => statement.Lines).Where(line => line.Type == PayLineType.Tax && taxCodes.Contains(line.TaxCode)).Sum(line => line.Amount);

    /// <summary>Gets the wages a tax was worked out on.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="taxCode">The tax code.</param>
    /// <returns>The wages.</returns>
    public static decimal Taxable(IEnumerable<PayStatement> statements, string taxCode)
        => statements.SelectMany(statement => statement.Lines).Where(line => line.Type == PayLineType.Tax && line.TaxCode == taxCode).Sum(line => line.TaxableWages ?? 0);

    /// <summary>Gets the tips reported (lines of a code of kind Tips).</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <returns>The tips.</returns>
    public static decimal Tips(IEnumerable<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes)
        => statements.SelectMany(statement => statement.Lines)
            .Where(line => line.Type is PayLineType.Earning or PayLineType.ImputedIncome && line.PayCodeId is not null && codes.TryGetValue(line.PayCodeId, out var code) && code.Kind == PayCodeKinds.Tips)
            .Sum(line => line.Amount);

    /// <summary>Gets the statements of employees (contractors are reported on their own form).</summary>
    /// <param name="context">The filing context.</param>
    /// <returns>The statements.</returns>
    public static IReadOnlyList<PayStatement> Employees(PayrollFilingContext context)
        => [.. context.Statements.Where(statement => !IsContractor(context, statement))];

    /// <summary>Gets the statements of contractors.</summary>
    /// <param name="context">The filing context.</param>
    /// <returns>The statements.</returns>
    public static IReadOnlyList<PayStatement> Contractors(PayrollFilingContext context)
        => [.. context.Statements.Where(statement => IsContractor(context, statement))];

    private static bool IsContractor(PayrollFilingContext context, PayStatement statement)
        => statement.ProfileId is not null && context.Profiles.TryGetValue(statement.ProfileId, out var profile) && profile.WorkerType == PayWorkerType.Contractor;

    /// <summary>Gets the deposits of a group of taxes.</summary>
    /// <param name="deposits">The deposits.</param>
    /// <param name="group">The group.</param>
    /// <returns>The amount.</returns>
    public static decimal Deposited(IEnumerable<TaxDeposit> deposits, string group)
        => deposits.Where(deposit => deposit.Group == group && string.Equals(deposit.Jurisdiction, UnitedStatesIncomeTax.Jurisdiction, StringComparison.OrdinalIgnoreCase)).Sum(deposit => deposit.Amount);
}

/// <summary>
/// Form 941, the employer's quarterly federal tax return: wages, income tax withheld, Social Security and Medicare
/// wages and taxes (both sides, by the rates of the form), the fractions-of-cents adjustment, the deposits and the
/// balance.
/// </summary>
public sealed class Form941 : IPayrollFilingForm
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<Form941>("Form 941 (quarterly federal return)");

    /// <summary>The form's stored name.</summary>
    public const string FormName = "US-941";

    /// <inheritdoc />
    public string Name => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public string Jurisdiction => UnitedStatesIncomeTax.Jurisdiction;

    /// <inheritdoc />
    public bool IsQuarterly => true;

    /// <inheritdoc />
    public bool HasPersonStatements => false;

    /// <inheritdoc />
    public Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var statements = UnitedStatesFilingSums.Employees(context);
        var boxes = context.Filing.Boxes;
        var twelfth = new DateOnly(context.To.Year, context.To.Month, 12);
        var tips = UnitedStatesFilingSums.Tips(statements, context.Codes);
        var socialSecurity = Math.Max(0, UnitedStatesFilingSums.Taxable(statements, UnitedStatesTaxCodes.SocialSecurity) - tips);
        var medicare = UnitedStatesFilingSums.Taxable(statements, UnitedStatesTaxCodes.Medicare);
        var additional = UnitedStatesFilingSums.Taxable(statements, UnitedStatesTaxCodes.AdditionalMedicare);

        boxes["1"] = statements.Where(statement => statement.VoidOf is null && statement.PeriodStart <= twelfth && statement.PeriodEnd >= twelfth && statement.Gross > 0).Select(statement => statement.StaffMemberId).Distinct(StringComparer.Ordinal).Count();
        boxes["2"] = UnitedStatesFilingSums.Wages(statements, context.Codes, IncomeTaxCategories.IncomeTax);
        boxes["3"] = UnitedStatesFilingSums.Tax(statements, UnitedStatesTaxCodes.IncomeTax);
        boxes["5a"] = socialSecurity;
        boxes["5a.tax"] = Math.Round(socialSecurity * 0.124m, 2, MidpointRounding.AwayFromZero);
        boxes["5b"] = tips;
        boxes["5b.tax"] = Math.Round(tips * 0.124m, 2, MidpointRounding.AwayFromZero);
        boxes["5c"] = medicare;
        boxes["5c.tax"] = Math.Round(medicare * 0.029m, 2, MidpointRounding.AwayFromZero);
        boxes["5d"] = additional;
        boxes["5d.tax"] = Math.Round(additional * 0.009m, 2, MidpointRounding.AwayFromZero);
        boxes["5e"] = boxes["5a.tax"] + boxes["5b.tax"] + boxes["5c.tax"] + boxes["5d.tax"];
        boxes["6"] = boxes["3"] + boxes["5e"];

        // What was withheld and matched, less what the form's rates give: the fractions of cents of each pay.
        boxes["7"] = UnitedStatesFilingSums.Tax(statements, UnitedStatesTaxCodes.SocialSecurity, UnitedStatesTaxCodes.SocialSecurityEmployer, UnitedStatesTaxCodes.Medicare, UnitedStatesTaxCodes.MedicareEmployer, UnitedStatesTaxCodes.AdditionalMedicare) - boxes["5e"];
        boxes["10"] = boxes["6"] + boxes["7"];
        boxes["12"] = boxes["10"];
        boxes["13"] = UnitedStatesFilingSums.Deposited(context.Deposits, TaxLiabilityGroups.Withholding);
        boxes["14"] = Math.Max(0, boxes["12"] - boxes["13"]);
        boxes["15"] = Math.Max(0, boxes["13"] - boxes["12"]);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Form 945, the annual return of federal income tax withheld from payments that are not wages: the backup withholding
/// on contractors' payments, the deposits made for it and the balance. It is kept out of Form 941.
/// </summary>
public sealed class Form945 : IPayrollFilingForm
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<Form945>("Form 945 (annual return of withheld federal income tax)");

    /// <summary>The form's stored name.</summary>
    public const string FormName = "US-945";

    /// <inheritdoc />
    public string Name => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public string Jurisdiction => UnitedStatesIncomeTax.Jurisdiction;

    /// <inheritdoc />
    public bool IsQuarterly => false;

    /// <inheritdoc />
    public bool HasPersonStatements => false;

    /// <inheritdoc />
    public Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var boxes = context.Filing.Boxes;

        boxes["1"] = 0;
        boxes["2"] = UnitedStatesFilingSums.Tax(UnitedStatesFilingSums.Contractors(context), Form1099Nec.BackupWithholdingCode);
        boxes["3"] = boxes["1"] + boxes["2"];
        boxes["4"] = UnitedStatesFilingSums.Deposited(context.Deposits, TaxLiabilityGroups.NonPayrollWithholding);
        boxes["5"] = Math.Max(0, boxes["3"] - boxes["4"]);
        boxes["6"] = Math.Max(0, boxes["4"] - boxes["3"]);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Form 940, the employer's annual federal unemployment return: payments to employees, the part exempt or over the
/// wage base, the taxable wages, the tax at the federal rule's rate, the deposits and the balance.
/// </summary>
public sealed class Form940 : IPayrollFilingForm
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<Form940>("Form 940 (annual federal unemployment return)");

    /// <summary>The form's stored name.</summary>
    public const string FormName = "US-940";

    /// <inheritdoc />
    public string Name => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public string Jurisdiction => UnitedStatesIncomeTax.Jurisdiction;

    /// <inheritdoc />
    public bool IsQuarterly => false;

    /// <inheritdoc />
    public bool HasPersonStatements => false;

    /// <inheritdoc />
    public async Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rule = context.Services.GetService<IIncomeTaxRuleManager>() is { } rules
            ? (await rules.ListAsync(cancellationToken)).FirstOrDefault(candidate => candidate.TaxCode == UnitedStatesTaxCodes.Unemployment && candidate.AppliesOn(context.To))
            : null;
        var wageBase = rule?.WageBase ?? 7000m;
        var rate = (rule?.Rate ?? 0.6m) / 100m;
        var boxes = context.Filing.Boxes;
        var employees = UnitedStatesFilingSums.Employees(context);
        var payments = employees.Sum(statement => statement.Gross);
        var subject = UnitedStatesFilingSums.Wages(employees, context.Codes, IncomeTaxCategories.Unemployment);
        var overBase = employees.GroupBy(statement => statement.StaffMemberId, StringComparer.Ordinal)
            .Sum(person => Math.Max(0, UnitedStatesFilingSums.Wages(person, context.Codes, IncomeTaxCategories.Unemployment) - wageBase));

        boxes["3"] = payments;
        boxes["4"] = Math.Max(0, payments - subject);
        boxes["5"] = overBase;
        boxes["6"] = boxes["4"] + boxes["5"];
        boxes["7"] = Math.Max(0, boxes["3"] - boxes["6"]);
        boxes["8"] = Math.Round(boxes["7"] * rate, 2, MidpointRounding.AwayFromZero);
        boxes["12"] = boxes["8"];
        boxes["13"] = UnitedStatesFilingSums.Deposited(context.Deposits, TaxLiabilityGroups.Unemployment);
        boxes["14"] = Math.Max(0, boxes["12"] - boxes["13"]);
        boxes["15"] = Math.Max(0, boxes["13"] - boxes["12"]);
    }
}

/// <summary>
/// Forms W-2, the wage and tax statement of each person for a year, and W-3, their totals: wages (box 1), income tax
/// (2), Social Security wages and tax (3, 4) and tips (7), Medicare wages and tax with the additional tax (5, 6), and
/// the box 12 amounts of the pay codes that name one (such as W2:12D). A person with no tax id on file is noted.
/// </summary>
public sealed class FormW2 : IPayrollFilingForm
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<FormW2>("Forms W-2 and W-3 (wage and tax statements)");

    /// <summary>The form's stored name.</summary>
    public const string FormName = "US-W2";

    /// <summary>The prefix of a pay code's form box for a W-2 box, such as W2:12D.</summary>
    public const string BoxPrefix = "W2:";

    /// <inheritdoc />
    public string Name => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public string Jurisdiction => UnitedStatesIncomeTax.Jurisdiction;

    /// <inheritdoc />
    public bool IsQuarterly => false;

    /// <inheritdoc />
    public bool HasPersonStatements => true;

    /// <inheritdoc />
    public Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var person in UnitedStatesFilingSums.Employees(context).GroupBy(statement => statement.ProfileId ?? statement.StaffMemberId, StringComparer.Ordinal).OrderBy(group => group.Last().Snapshot?.EmployeeName, StringComparer.CurrentCultureIgnoreCase))
        {
            var statements = person.ToList();
            var latest = statements.Where(statement => statement.VoidOf is null).OrderBy(statement => statement.PayDate).LastOrDefault() ?? statements.Last();
            var profile = latest.ProfileId is not null && context.Profiles.TryGetValue(latest.ProfileId, out var found) ? found : null;
            var boxes = Boxes(statements, context.Codes);

            if (boxes.Values.All(value => value == 0))
            {
                continue;
            }

            if (profile?.TaxId is null)
            {
                context.Filing.Notes.Add($"{latest.Snapshot?.EmployeeName}: no tax id on file.");
            }

            context.Filing.People.Add(new PayrollFilingPerson
            {
                StaffMemberId = latest.StaffMemberId,
                ProfileId = latest.ProfileId,
                Name = string.IsNullOrEmpty(profile?.LegalName) ? latest.Snapshot?.EmployeeName : profile.LegalName,
                Address = [.. latest.Snapshot?.EmployeeAddress ?? []],
                TaxIdHint = profile?.TaxId?.Hint,
                Boxes = boxes,
            });
        }

        // W-3: the totals of the wage statements.
        foreach (var person in context.Filing.People)
        {
            foreach (var (box, amount) in person.Boxes)
            {
                context.Filing.Boxes[box] = context.Filing.Boxes.GetValueOrDefault(box) + amount;
            }
        }

        context.Filing.Boxes["c"] = context.Filing.People.Count;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the W-2 boxes of one person's statements of a year.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <returns>The boxes.</returns>
    public static Dictionary<string, decimal> Boxes(IReadOnlyList<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(codes);

        var tips = UnitedStatesFilingSums.Tips(statements, codes);
        var socialSecurity = UnitedStatesFilingSums.Taxable(statements, UnitedStatesTaxCodes.SocialSecurity);
        var boxes = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["1"] = UnitedStatesFilingSums.Wages(statements, codes, IncomeTaxCategories.IncomeTax),
            ["2"] = UnitedStatesFilingSums.Tax(statements, UnitedStatesTaxCodes.IncomeTax),
            ["3"] = Math.Max(0, socialSecurity - tips),
            ["4"] = UnitedStatesFilingSums.Tax(statements, UnitedStatesTaxCodes.SocialSecurity),
            ["5"] = UnitedStatesFilingSums.Taxable(statements, UnitedStatesTaxCodes.Medicare),
            ["6"] = UnitedStatesFilingSums.Tax(statements, UnitedStatesTaxCodes.Medicare, UnitedStatesTaxCodes.AdditionalMedicare),
            ["7"] = Math.Min(tips, socialSecurity),
        };

        foreach (var line in statements.SelectMany(statement => statement.Lines))
        {
            if (line.PayCodeId is null || !codes.TryGetValue(line.PayCodeId, out var code))
            {
                continue;
            }

            // A reversal's lines are negative, so a voided amount cancels.
            foreach (var box in code.GetPayrollSettings().FormBoxes.Where(box => box.StartsWith(BoxPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                var key = box[BoxPrefix.Length..];

                boxes[key] = boxes.GetValueOrDefault(key) + line.Amount;
            }
        }

        return boxes;
    }
}

/// <summary>
/// Form 1099-NEC, nonemployee compensation, of each contractor paid at least the reporting threshold in a year (2,000
/// from 2026, 600 before) or withheld on, and the Form 1096 totals: box 1 the contractor's payments, box 4 the backup
/// withholding. A contractor with no tax id on file is noted.
/// </summary>
public sealed class Form1099Nec : IPayrollFilingForm
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<Form1099Nec>("Forms 1099-NEC and 1096 (nonemployee compensation)");

    /// <summary>The form's stored name.</summary>
    public const string FormName = "US-1099NEC";

    /// <summary>The tax code of backup withholding.</summary>
    public const string BackupWithholdingCode = "US-BWH";

    /// <inheritdoc />
    public string Name => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public string Jurisdiction => UnitedStatesIncomeTax.Jurisdiction;

    /// <inheritdoc />
    public bool IsQuarterly => false;

    /// <inheritdoc />
    public bool HasPersonStatements => true;

    /// <summary>
    /// Gets the payments a year that call for the form (the One Big Beautiful Bill Act raised it from 600 to 2,000 for
    /// payments from 2026; it is indexed for inflation from 2027).
    /// </summary>
    /// <param name="year">The year.</param>
    /// <returns>The threshold.</returns>
    public static decimal ThresholdFor(int year)
        => year >= 2026 ? 2000m : 600m;

    /// <inheritdoc />
    public Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var threshold = ThresholdFor(context.Filing.Year);

        foreach (var contractor in UnitedStatesFilingSums.Contractors(context).GroupBy(statement => statement.ProfileId, StringComparer.Ordinal))
        {
            var statements = contractor.ToList();
            var profile = context.Profiles[contractor.Key];
            var paid = statements.Sum(statement => statement.Gross);
            var withheld = UnitedStatesFilingSums.Tax(statements, BackupWithholdingCode);

            if (paid < threshold && withheld <= 0)
            {
                continue;
            }

            if (profile.TaxId is null)
            {
                context.Filing.Notes.Add($"{statements[^1].Snapshot?.EmployeeName}: no tax id on file.");
            }

            context.Filing.People.Add(new PayrollFilingPerson
            {
                StaffMemberId = profile.StaffMemberId,
                ProfileId = profile.ItemId,
                Name = string.IsNullOrEmpty(profile.LegalName) ? statements[^1].Snapshot?.EmployeeName : profile.LegalName,
                Address = [.. statements[^1].Snapshot?.EmployeeAddress ?? []],
                TaxIdHint = profile.TaxId?.Hint,
                Boxes = new Dictionary<string, decimal>(StringComparer.Ordinal) { ["1"] = paid, ["4"] = withheld },
            });
        }

        // Form 1096: the number of forms and their totals.
        context.Filing.Boxes["3"] = context.Filing.People.Count;
        context.Filing.Boxes["4"] = context.Filing.People.Sum(person => person.Boxes["4"]);
        context.Filing.Boxes["5"] = context.Filing.People.Sum(person => person.Boxes["1"]);

        return Task.CompletedTask;
    }
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

/// <summary>
/// The texts of a printed wage statement, in the reader's language.
/// </summary>
public sealed class FormW2Texts
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = "Form W-2 Wage and Tax Statement";

    /// <summary>Gets or sets the copy's name.</summary>
    public string Copy { get; set; } = "Copy B, to be filed with the employee's federal tax return; Copies C and 2 for the employee's records";

    /// <summary>Gets or sets the label of each box, by its number.</summary>
    public IDictionary<string, string> Boxes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1"] = "1 Wages, tips, other compensation",
        ["2"] = "2 Federal income tax withheld",
        ["3"] = "3 Social security wages",
        ["4"] = "4 Social security tax withheld",
        ["5"] = "5 Medicare wages and tips",
        ["6"] = "6 Medicare tax withheld",
        ["7"] = "7 Social security tips",
    };

    /// <summary>Gets or sets the label of the employee's tax id.</summary>
    public string TaxId { get; set; } = "Employee's social security number";

    /// <summary>Gets or sets the label of the employer's tax id.</summary>
    public string EmployerTaxId { get; set; } = "Employer identification number (EIN)";
}

/// <summary>
/// The texts of a printed Form 1099-NEC, in the reader's language.
/// </summary>
public sealed class Form1099NecTexts
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = "Form 1099-NEC Nonemployee Compensation";

    /// <summary>Gets or sets the copy's name.</summary>
    public string Copy { get; set; } = "Copy B, for the recipient";

    /// <summary>Gets or sets the payer's tax id label.</summary>
    public string PayerTaxId { get; set; } = "Payer's TIN";

    /// <summary>Gets or sets the recipient's tax id label.</summary>
    public string RecipientTaxId { get; set; } = "Recipient's TIN";

    /// <summary>Gets or sets box 1's label.</summary>
    public string Box1 { get; set; } = "1 Nonemployee compensation";

    /// <summary>Gets or sets box 4's label.</summary>
    public string Box4 { get; set; } = "4 Federal income tax withheld";
}

/// <summary>
/// Prints a contractor's Form 1099-NEC for their records: the payer, the recipient (the tax id by its last characters),
/// box 1 and box 4.
/// </summary>
public static class Form1099NecDocument
{
    private const double Margin = 48;

    /// <summary>Writes the PDF.</summary>
    /// <param name="filing">The filing.</param>
    /// <param name="person">The contractor's statement.</param>
    /// <param name="employer">The payer.</param>
    /// <param name="payerAddress">The payer's address lines.</param>
    /// <param name="texts">The texts.</param>
    /// <returns>The PDF.</returns>
    public static byte[] Write(PayrollFiling filing, PayrollFilingPerson person, Employer employer, IReadOnlyList<string> payerAddress, Form1099NecTexts texts)
    {
        ArgumentNullException.ThrowIfNull(filing);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(texts);

        using var writer = new PdfDocumentWriter(texts.Title);
        var regular = writer.AddStandardFont();
        var bold = writer.AddStandardFont(bold: true);
        var page = writer.AddPage(PdfPageSize.Letter);
        var right = PdfPageSize.Letter.Width - Margin;
        var y = Margin;

        void Text(string text, PdfFontFace font, double size, double x, double top)
            => page.DrawText(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, x, top, PdfColor.Black);

        void TextRight(string text, PdfFontFace font, double size, double at, double top)
            => page.DrawTextRight(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, at, top, PdfColor.Black);

        Text(texts.Title, bold, 14, Margin, y);
        TextRight(filing.Year.ToString(CultureInfo.InvariantCulture), bold, 14, right, y);
        y += 18;
        Text(texts.Copy, regular, 7, Margin, y);
        y += 20;
        Text(employer?.PrintedName, bold, 10, Margin, y);
        TextRight($"{texts.PayerTaxId}: {employer?.TaxId}", regular, 8, right, y);
        y += 13;

        foreach (var line in payerAddress)
        {
            Text(line, regular, 9, Margin, y);
            y += 12;
        }

        y += 10;
        Text(person.Name, bold, 10, Margin, y);
        TextRight($"{texts.RecipientTaxId}: {(string.IsNullOrEmpty(person.TaxIdHint) ? "-" : "XXXXX" + person.TaxIdHint)}", regular, 8, right, y);
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

        page.DrawRectangle(Margin, y, half - 6, 30, PdfColor.Black, 0.5);
        Text(texts.Box1, regular, 7, Margin + 4, y + 3);
        TextRight(person.Boxes.GetValueOrDefault("1").ToString("N2", CultureInfo.InvariantCulture), bold, 10, Margin + half - 10, y + 15);
        page.DrawRectangle(Margin + half, y, half - 6, 30, PdfColor.Black, 0.5);
        Text(texts.Box4, regular, 7, Margin + half + 4, y + 3);
        TextRight(person.Boxes.GetValueOrDefault("4").ToString("N2", CultureInfo.InvariantCulture), bold, 10, right - 10, y + 15);

        return writer.Build();
    }
}

/// <summary>
/// Prints a person's wage statement for their records: the employer, the person (the tax id shown by its last
/// characters), and the boxes, with box 12 amounts by their code.
/// </summary>
public static class FormW2Document
{
    private const double Margin = 48;

    /// <summary>
    /// Writes the PDF.
    /// </summary>
    /// <param name="filing">The filing.</param>
    /// <param name="person">The person's statement.</param>
    /// <param name="employer">The employer.</param>
    /// <param name="employerAddress">The employer's address lines.</param>
    /// <param name="texts">The texts.</param>
    /// <returns>The PDF.</returns>
    public static byte[] Write(PayrollFiling filing, PayrollFilingPerson person, Employer employer, IReadOnlyList<string> employerAddress, FormW2Texts texts)
    {
        ArgumentNullException.ThrowIfNull(filing);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(texts);

        using var writer = new PdfDocumentWriter(texts.Title);
        var regular = writer.AddStandardFont();
        var bold = writer.AddStandardFont(bold: true);
        var page = writer.AddPage(PdfPageSize.Letter);
        var right = PdfPageSize.Letter.Width - Margin;
        var y = Margin;

        void Text(string text, PdfFontFace font, double size, double x, double top)
            => page.DrawText(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, x, top, PdfColor.Black);

        void TextRight(string text, PdfFontFace font, double size, double at, double top)
            => page.DrawTextRight(font.Prepare(PdfText.Clean(text ?? string.Empty)), font, size, at, top, PdfColor.Black);

        Text(texts.Title, bold, 14, Margin, y);
        TextRight(filing.Year.ToString(CultureInfo.InvariantCulture), bold, 14, right, y);
        y += 18;
        Text(texts.Copy, regular, 7, Margin, y);
        y += 20;

        Text(employer?.PrintedName, bold, 10, Margin, y);
        TextRight($"{texts.EmployerTaxId}: {employer?.TaxId}", regular, 8, right, y);
        y += 13;

        foreach (var line in employerAddress)
        {
            Text(line, regular, 9, Margin, y);
            y += 12;
        }

        y += 10;
        Text(person.Name, bold, 10, Margin, y);
        TextRight($"{texts.TaxId}: XXX-XX-{person.TaxIdHint}", regular, 8, right, y);
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
        var boxes = texts.Boxes.Keys.ToList();

        for (var i = 0; i < boxes.Count; i += 2)
        {
            for (var column = 0; column < 2 && i + column < boxes.Count; column++)
            {
                var box = boxes[i + column];
                var x = Margin + (column * half);

                page.DrawRectangle(x, y, half - 6, 30, PdfColor.Black, 0.5);
                Text(texts.Boxes[box], regular, 7, x + 4, y + 3);
                TextRight(person.Boxes.GetValueOrDefault(box).ToString("N2", CultureInfo.InvariantCulture), bold, 10, x + half - 10, y + 15);
            }

            y += 36;
        }

        foreach (var (box, amount) in person.Boxes.Where(pair => pair.Key.StartsWith("12", StringComparison.Ordinal) && pair.Value != 0))
        {
            Text($"12{box[2..]}  {amount.ToString("N2", CultureInfo.InvariantCulture)}", regular, 9, Margin, y);
            y += 12;
        }

        return writer.Build();
    }
}
