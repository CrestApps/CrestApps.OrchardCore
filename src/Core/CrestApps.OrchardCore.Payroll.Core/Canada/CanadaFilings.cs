using System.Globalization;
using System.Text;
using System.Xml;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Canada;

/// <summary>
/// The tax codes of the Canada Income Tax recipes the Canadian filings read, and what they share.
/// </summary>
public static class CanadaFilingSums
{
    /// <summary>The suffix of an employer's share of a contribution.</summary>
    public const string EmployerSuffix = "-ER";

    /// <summary>The suffix of a province's or territory's income tax.</summary>
    public const string IncomeTaxSuffix = "-PIT";

    /// <summary>Gets the CRA business number with its payroll program account (RP), from the employer's CA registration.</summary>
    /// <param name="employer">The employer.</param>
    /// <returns>The account, or <see langword="null"/>.</returns>
    public static string PayrollAccount(Employer employer)
        => Account(employer, CanadaIncomeTax.Jurisdiction);

    /// <summary>Gets the Revenu Québec identification number, from the employer's CA-QC registration.</summary>
    /// <param name="employer">The employer.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    public static string QuebecAccount(Employer employer)
        => Account(employer, CanadaIncomeTax.Quebec);

    /// <summary>
    /// Gets whether a payroll account is a business number with a payroll program account: nine digits, RP and four
    /// digits (123456789RP0001).
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsPayrollAccount(string account)
        => Normalize(account) is { Length: 15 } value && value[..9].All(char.IsAsciiDigit) && value[9..11] == "RP" && value[11..].All(char.IsAsciiDigit);

    /// <summary>
    /// Gets whether a Revenu Québec identification number is ten digits, TQ and four digits (1234567890TQ0001).
    /// </summary>
    /// <param name="account">The number.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsQuebecAccount(string account)
        => Normalize(account) is { Length: 16 } value && value[..10].All(char.IsAsciiDigit) && value[10..12] == "TQ" && value[12..].All(char.IsAsciiDigit);

    /// <summary>Gets an account without spaces and dashes, in capitals.</summary>
    /// <param name="account">The account.</param>
    /// <returns>The account.</returns>
    public static string Normalize(string account)
        => account is null ? null : new string([.. account.Where(character => !char.IsWhiteSpace(character) && character != '-')]).ToUpperInvariant();

    /// <summary>Gets the statements of a filing whose people paid a tax of a jurisdiction (CA, or CA-QC), employees only.</summary>
    /// <param name="context">The filing context.</param>
    /// <param name="jurisdiction">The jurisdiction prefix.</param>
    /// <returns>The statements, by person.</returns>
    public static IEnumerable<IGrouping<string, PayStatement>> People(PayrollFilingContext context, string jurisdiction)
    {
        var employees = UnitedStatesFilingSums.Employees(context);
        var people = employees
            .Where(statement => statement.Lines.Any(line => line.Type == PayLineType.Tax && line.Jurisdiction?.StartsWith(jurisdiction, StringComparison.OrdinalIgnoreCase) == true))
            .Select(statement => statement.ProfileId ?? statement.StaffMemberId)
            .ToHashSet(StringComparer.Ordinal);

        return employees
            .Where(statement => people.Contains(statement.ProfileId ?? statement.StaffMemberId))
            .GroupBy(statement => statement.ProfileId ?? statement.StaffMemberId, StringComparer.Ordinal)
            .OrderBy(group => group.Last().Snapshot?.EmployeeName, StringComparer.CurrentCultureIgnoreCase);
    }

    /// <summary>Gets the amount of the provincial and territorial income taxes the CRA collects (every CA-XX-PIT but Quebec's).</summary>
    /// <param name="statements">The statements.</param>
    /// <returns>The amount.</returns>
    public static decimal ProvincialIncomeTax(IEnumerable<PayStatement> statements)
        => statements.SelectMany(statement => statement.Lines)
            .Where(line => line.Type == PayLineType.Tax && line.TaxCode?.StartsWith("CA-", StringComparison.Ordinal) == true && line.TaxCode.EndsWith(IncomeTaxSuffix, StringComparison.Ordinal) && line.TaxCode != CanadaIncomeTax.TaxCodes.QuebecIncomeTax)
            .Sum(line => line.Amount);

    /// <summary>Gets the province of employment of a person (the jurisdiction of their provincial tax), as its two letters.</summary>
    /// <param name="statements">The statements.</param>
    /// <returns>The province, such as ON, or <see langword="null"/>.</returns>
    public static string Province(IEnumerable<PayStatement> statements)
        => statements.SelectMany(statement => statement.Lines)
            .Where(line => line.Type == PayLineType.Tax && line.Jurisdiction?.StartsWith("CA-", StringComparison.OrdinalIgnoreCase) == true && line.Jurisdiction.Length == 5)
            .Select(line => line.Jurisdiction[3..].ToUpperInvariant())
            .LastOrDefault();

    /// <summary>Gets the deposits remitted to an agency.</summary>
    /// <param name="deposits">The deposits.</param>
    /// <param name="jurisdiction">The agency's jurisdiction.</param>
    /// <returns>The amount.</returns>
    public static decimal Remitted(IEnumerable<TaxDeposit> deposits, string jurisdiction)
        => deposits.Where(deposit => string.Equals(deposit.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase)).Sum(deposit => deposit.Amount);

    private static string Account(Employer employer, string jurisdiction)
        => employer?.Registrations.FirstOrDefault(registration => string.Equals(registration.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(registration.AccountId))?.AccountId?.Trim();
}

/// <summary>
/// The T4 slips (statement of remuneration paid) of each employee paid Canadian source deductions in a year, and the T4
/// Summary: box 14 employment income, 16 and 16A CPP, 17 and 17A QPP, 18 EI, 22 income tax deducted (federal and the
/// provinces the CRA collects for, never Quebec's), 24 EI insurable earnings, 26 CPP or QPP pensionable earnings, 55 and
/// 56 QPIP; the Summary adds the employer's CPP (27, 27A) and EI (19), the total deductions (80), the remittances to the
/// CRA (82) and the balance (84 overpayment or 86 due), with the slips' count (88).
/// </summary>
public sealed class FormT4 : IPayrollFilingForm
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<FormT4>("T4 slips and T4 Summary (remuneration paid)");

    /// <summary>The form's stored name.</summary>
    public const string FormName = "CA-T4";

    /// <inheritdoc />
    public string Name => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public string Jurisdiction => CanadaIncomeTax.Jurisdiction;

    /// <inheritdoc />
    public bool IsQuarterly => false;

    /// <inheritdoc />
    public bool HasPersonStatements => true;

    /// <inheritdoc />
    public Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!CanadaFilingSums.IsPayrollAccount(CanadaFilingSums.PayrollAccount(context.Employer)))
        {
            context.Filing.Notes.Add("The employer's CA registration needs the CRA business number with its payroll program account, such as 123456789RP0001.");
        }

        var employerCpp = 0m;
        var employerCpp2 = 0m;
        var employerEi = 0m;

        foreach (var person in CanadaFilingSums.People(context, CanadaIncomeTax.Jurisdiction))
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
                context.Filing.Notes.Add($"{latest.Snapshot?.EmployeeName}: no social insurance number on file.");
            }

            employerCpp += UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Cpp + CanadaFilingSums.EmployerSuffix);
            employerCpp2 += UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Cpp2 + CanadaFilingSums.EmployerSuffix);
            employerEi += UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.EmploymentInsurance + CanadaFilingSums.EmployerSuffix, CanadaIncomeTax.TaxCodes.EmploymentInsuranceQuebec + CanadaFilingSums.EmployerSuffix);

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

        // The T4 Summary: the slips' totals, the employer's contributions, the remittances and the balance.
        foreach (var box in new[] { "14", "16", "16A", "17", "17A", "18", "22", "24", "26", "55", "56" })
        {
            context.Filing.Boxes[box] = context.Filing.People.Sum(person => person.Boxes.GetValueOrDefault(box));
        }

        context.Filing.Boxes["19"] = employerEi;
        context.Filing.Boxes["27"] = employerCpp;
        context.Filing.Boxes["27A"] = employerCpp2;

        var deductions = context.Filing.Boxes["16"] + context.Filing.Boxes["16A"] + context.Filing.Boxes["18"] + employerEi + context.Filing.Boxes["22"] + employerCpp + employerCpp2;
        var remitted = CanadaFilingSums.Remitted(context.Deposits, CanadaIncomeTax.Jurisdiction);

        context.Filing.Boxes["80"] = deductions;
        context.Filing.Boxes["82"] = remitted;
        context.Filing.Boxes["84"] = Math.Max(0, remitted - deductions);
        context.Filing.Boxes["86"] = Math.Max(0, deductions - remitted);
        context.Filing.Boxes["88"] = context.Filing.People.Count;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the T4 boxes of one person's statements of a year.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <returns>The boxes.</returns>
    public static Dictionary<string, decimal> Boxes(IReadOnlyList<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(codes);

        var quebec = statements.Any(statement => statement.Lines.Any(line => line.TaxCode == CanadaIncomeTax.TaxCodes.Qpp));

        return new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["14"] = UnitedStatesFilingSums.Wages(statements, codes, IncomeTaxCategories.IncomeTax),
            ["16"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Cpp),
            ["16A"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Cpp2),
            ["17"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpp),
            ["17A"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpp2),
            ["18"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.EmploymentInsurance, CanadaIncomeTax.TaxCodes.EmploymentInsuranceQuebec),
            ["22"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.FederalIncomeTax) + CanadaFilingSums.ProvincialIncomeTax(statements),
            ["24"] = UnitedStatesFilingSums.Taxable(statements, CanadaIncomeTax.TaxCodes.EmploymentInsurance) + UnitedStatesFilingSums.Taxable(statements, CanadaIncomeTax.TaxCodes.EmploymentInsuranceQuebec),
            ["26"] = UnitedStatesFilingSums.Taxable(statements, quebec ? CanadaIncomeTax.TaxCodes.Qpp : CanadaIncomeTax.TaxCodes.Cpp),
            ["55"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpip),
            ["56"] = UnitedStatesFilingSums.Taxable(statements, CanadaIncomeTax.TaxCodes.Qpip),
        };
    }
}

/// <summary>
/// The RL-1 slips (employment and other income) of each employee paid Quebec source deductions in a year, and the
/// RL-1 Summary: box A employment income, B.A QPP, B.B QPP2, C EI, E Quebec income tax withheld, G QPP pensionable
/// salary, H QPIP and I QPIP insurable salary; the Summary adds the employer's QPP, QPP2 and QPIP, the remittances to
/// Revenu Québec and the balance.
/// </summary>
public sealed class FormRl1 : IPayrollFilingForm
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<FormRl1>("RL-1 slips and RL-1 Summary (Revenu Québec)");

    /// <summary>The form's stored name.</summary>
    public const string FormName = "CA-RL1";

    /// <inheritdoc />
    public string Name => FormName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public string Jurisdiction => CanadaIncomeTax.Quebec;

    /// <inheritdoc />
    public bool IsQuarterly => false;

    /// <inheritdoc />
    public bool HasPersonStatements => true;

    /// <inheritdoc />
    public Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!CanadaFilingSums.IsQuebecAccount(CanadaFilingSums.QuebecAccount(context.Employer)))
        {
            context.Filing.Notes.Add("The employer's CA-QC registration needs the Revenu Québec identification number, such as 1234567890TQ0001.");
        }

        var employerQpp = 0m;
        var employerQpp2 = 0m;
        var employerQpip = 0m;

        foreach (var person in CanadaFilingSums.People(context, CanadaIncomeTax.Quebec))
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
                context.Filing.Notes.Add($"{latest.Snapshot?.EmployeeName}: no social insurance number on file.");
            }

            employerQpp += UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpp + CanadaFilingSums.EmployerSuffix);
            employerQpp2 += UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpp2 + CanadaFilingSums.EmployerSuffix);
            employerQpip += UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpip + CanadaFilingSums.EmployerSuffix);

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

        foreach (var box in new[] { "A", "B.A", "B.B", "C", "E", "G", "H", "I" })
        {
            context.Filing.Boxes[box] = context.Filing.People.Sum(person => person.Boxes.GetValueOrDefault(box));
        }

        context.Filing.Boxes["QPP.Employer"] = employerQpp;
        context.Filing.Boxes["QPP2.Employer"] = employerQpp2;
        context.Filing.Boxes["QPIP.Employer"] = employerQpip;

        var owed = context.Filing.Boxes["B.A"] + context.Filing.Boxes["B.B"] + context.Filing.Boxes["E"] + context.Filing.Boxes["H"] + employerQpp + employerQpp2 + employerQpip;
        var remitted = CanadaFilingSums.Remitted(context.Deposits, CanadaIncomeTax.Quebec);

        context.Filing.Boxes["Owed"] = owed;
        context.Filing.Boxes["Remitted"] = remitted;
        context.Filing.Boxes["Balance"] = owed - remitted;
        context.Filing.Boxes["Slips"] = context.Filing.People.Count;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the RL-1 boxes of one person's statements of a year.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <returns>The boxes.</returns>
    public static Dictionary<string, decimal> Boxes(IReadOnlyList<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(codes);

        return new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["A"] = UnitedStatesFilingSums.Wages(statements, codes, IncomeTaxCategories.IncomeTax),
            ["B.A"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpp),
            ["B.B"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpp2),
            ["C"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.EmploymentInsuranceQuebec),
            ["E"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.QuebecIncomeTax),
            ["G"] = UnitedStatesFilingSums.Taxable(statements, CanadaIncomeTax.TaxCodes.Qpp),
            ["H"] = UnitedStatesFilingSums.Tax(statements, CanadaIncomeTax.TaxCodes.Qpip),
            ["I"] = UnitedStatesFilingSums.Taxable(statements, CanadaIncomeTax.TaxCodes.Qpip),
        };
    }
}

/// <summary>A T4 slip as the T4 XML file wants it.</summary>
/// <param name="Sin">The social insurance number, nine digits.</param>
/// <param name="FirstName">The given name.</param>
/// <param name="LastName">The surname.</param>
/// <param name="AddressLine">The street address.</param>
/// <param name="City">The city.</param>
/// <param name="Province">The province's two letters.</param>
/// <param name="Country">The country's three letters (CAN).</param>
/// <param name="PostalCode">The postal code.</param>
/// <param name="ProvinceOfEmployment">Box 10, the province of employment.</param>
/// <param name="EmployeeNumber">The employee's number.</param>
/// <param name="Boxes">The amounts, by box.</param>
public sealed record T4XmlSlip(string Sin, string FirstName, string LastName, string AddressLine, string City, string Province, string Country, string PostalCode, string ProvinceOfEmployment, string EmployeeNumber, IReadOnlyDictionary<string, decimal> Boxes);

/// <summary>The employer of a T4 XML file.</summary>
/// <param name="BusinessNumber">The business number with its payroll program account (123456789RP0001).</param>
/// <param name="Name">The employer's name.</param>
/// <param name="AddressLine">The street address.</param>
/// <param name="City">The city.</param>
/// <param name="Province">The province's two letters.</param>
/// <param name="Country">The country's three letters.</param>
/// <param name="PostalCode">The postal code.</param>
/// <param name="ContactName">The contact's name.</param>
/// <param name="ContactPhone">The contact's phone.</param>
/// <param name="ContactEmail">The contact's email.</param>
public sealed record T4XmlEmployer(string BusinessNumber, string Name, string AddressLine, string City, string Province, string Country, string PostalCode, string ContactName, string ContactPhone, string ContactEmail);

/// <summary>
/// Writes the CRA's T4 XML file (Internet file transfer): a Submission with its T619 transmittal and a Return of T4 slips
/// and the T4 Summary, with the element names of the CRA's T4 XML specification. One employer and one tax year a file.
/// </summary>
public static class CanadaT4Xml
{
    /// <summary>The slip's amounts, by box, and their element names.</summary>
    public static readonly IReadOnlyList<(string Box, string Element)> SlipAmounts =
    [
        ("14", "empt_incamt"),
        ("16", "cpp_cntrb_amt"),
        ("16A", "cppe_cntrb_amt"),
        ("17", "qpp_cntrb_amt"),
        ("17A", "qppe_cntrb_amt"),
        ("18", "empe_eip_amt"),
        ("22", "itx_ddct_amt"),
        ("24", "ei_insu_ern_amt"),
        ("26", "cpp_qpp_ern_amt"),
        ("55", "prov_pip_amt"),
        ("56", "prov_insu_ern_amt"),
    ];

    /// <summary>
    /// Writes the file.
    /// </summary>
    /// <param name="employer">The employer, who is also the transmitter.</param>
    /// <param name="slips">The slips.</param>
    /// <param name="year">The tax year.</param>
    /// <param name="summary">The T4 Summary's amounts, by line (19, 27 and 27A are the employer's).</param>
    /// <returns>The XML.</returns>
    public static string Write(T4XmlEmployer employer, IReadOnlyList<T4XmlSlip> slips, int year, IReadOnlyDictionary<string, decimal> summary)
    {
        ArgumentNullException.ThrowIfNull(employer);
        ArgumentNullException.ThrowIfNull(slips);
        ArgumentNullException.ThrowIfNull(summary);

        var builder = new StringBuilder();

        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8, OmitXmlDeclaration = false }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Submission");
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

            writer.WriteStartElement("T619");
            writer.WriteStartElement("TransmitterAccountNumber");
            writer.WriteElementString("bn15", CanadaFilingSums.Normalize(employer.BusinessNumber));
            writer.WriteEndElement();
            writer.WriteElementString("sbmt_ref_id", $"T4{year}");
            writer.WriteElementString("summ_cnt", "1");
            writer.WriteElementString("lang_cd", "E");
            writer.WriteStartElement("TransmitterName");
            writer.WriteElementString("l1_nm", Cut(employer.Name, 30));
            writer.WriteEndElement();
            writer.WriteElementString("TransmitterCountryCode", "CAN");
            Contact(writer, employer, email: true);
            writer.WriteEndElement();

            writer.WriteStartElement("Return");
            writer.WriteStartElement("T4");

            foreach (var slip in slips)
            {
                writer.WriteStartElement("T4Slip");
                writer.WriteStartElement("EMPE_NM");
                writer.WriteElementString("snm", Cut(slip.LastName, 20));
                writer.WriteElementString("gvn_nm", Cut(slip.FirstName, 12));
                writer.WriteEndElement();
                writer.WriteStartElement("EMPE_ADDR");
                writer.WriteElementString("addr_l1_txt", Cut(slip.AddressLine, 30));
                writer.WriteElementString("cty_nm", Cut(slip.City, 28));
                writer.WriteElementString("prov_cd", slip.Province);
                writer.WriteElementString("cntry_cd", slip.Country ?? "CAN");
                writer.WriteElementString("pstl_cd", CanadaFilingSums.Normalize(slip.PostalCode));
                writer.WriteEndElement();
                writer.WriteElementString("sin", Digits(slip.Sin));

                if (!string.IsNullOrEmpty(slip.EmployeeNumber))
                {
                    writer.WriteElementString("empe_nbr", Cut(slip.EmployeeNumber, 20));
                }

                writer.WriteElementString("bn", CanadaFilingSums.Normalize(employer.BusinessNumber));
                writer.WriteElementString("rpt_tcd", "O");
                writer.WriteElementString("empt_prov_cd", slip.ProvinceOfEmployment);
                writer.WriteStartElement("T4_AMT");

                foreach (var (box, element) in SlipAmounts)
                {
                    if (slip.Boxes.GetValueOrDefault(box) is var amount && amount != 0)
                    {
                        writer.WriteElementString(element, Money(amount));
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteStartElement("T4Summary");
            writer.WriteElementString("bn", CanadaFilingSums.Normalize(employer.BusinessNumber));
            writer.WriteStartElement("EMPR_NM");
            writer.WriteElementString("l1_nm", Cut(employer.Name, 30));
            writer.WriteEndElement();
            writer.WriteStartElement("EMPR_ADDR");
            writer.WriteElementString("addr_l1_txt", Cut(employer.AddressLine, 30));
            writer.WriteElementString("cty_nm", Cut(employer.City, 28));
            writer.WriteElementString("prov_cd", employer.Province);
            writer.WriteElementString("cntry_cd", employer.Country ?? "CAN");
            writer.WriteElementString("pstl_cd", CanadaFilingSums.Normalize(employer.PostalCode));
            writer.WriteEndElement();
            Contact(writer, employer, email: false);
            writer.WriteElementString("tx_yr", year.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("slp_cnt", slips.Count.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("rpt_tcd", "O");
            writer.WriteStartElement("T4_TAMT");
            writer.WriteElementString("tot_empt_incamt", Money(slips.Sum(slip => slip.Boxes.GetValueOrDefault("14"))));
            writer.WriteElementString("tot_empe_cpp_amt", Money(slips.Sum(slip => slip.Boxes.GetValueOrDefault("16"))));
            writer.WriteElementString("tot_empe_cppe_amt", Money(slips.Sum(slip => slip.Boxes.GetValueOrDefault("16A"))));
            writer.WriteElementString("tot_empe_eip_amt", Money(slips.Sum(slip => slip.Boxes.GetValueOrDefault("18"))));
            writer.WriteElementString("tot_itx_ddct_amt", Money(slips.Sum(slip => slip.Boxes.GetValueOrDefault("22"))));
            writer.WriteElementString("tot_empr_cpp_amt", Money(summary.GetValueOrDefault("27")));
            writer.WriteElementString("tot_empr_cppe_amt", Money(summary.GetValueOrDefault("27A")));
            writer.WriteElementString("tot_empr_eip_amt", Money(summary.GetValueOrDefault("19")));
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return builder.ToString();
    }

    private static void Contact(XmlWriter writer, T4XmlEmployer employer, bool email)
    {
        var phone = Digits(employer.ContactPhone);

        writer.WriteStartElement("CNTC");
        writer.WriteElementString("cntc_nm", Cut(employer.ContactName, 22));

        if (phone.Length >= 10)
        {
            phone = phone[^10..];
            writer.WriteElementString("cntc_area_cd", phone[..3]);
            writer.WriteElementString("cntc_phn_nbr", $"{phone[3..6]}-{phone[6..]}");
        }

        if (email && !string.IsNullOrEmpty(employer.ContactEmail))
        {
            writer.WriteElementString("cntc_email_area", Cut(employer.ContactEmail, 60));
        }

        writer.WriteEndElement();
    }

    private static string Money(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Digits(string value)
        => new([.. (value ?? string.Empty).Where(char.IsAsciiDigit)]);

    private static string Cut(string value, int length)
        => value is null ? string.Empty : (value.Length > length ? value[..length] : value);
}
