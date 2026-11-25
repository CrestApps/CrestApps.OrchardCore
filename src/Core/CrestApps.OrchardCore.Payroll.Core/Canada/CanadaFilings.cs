using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Canada;

/// <summary>
/// The employer account numbers the Canadian filings print and check.
/// </summary>
public static class CanadaFilingSums
{
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

    private static string Account(Employer employer, string jurisdiction)
        => employer?.Registrations.FirstOrDefault(registration => string.Equals(registration.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(registration.AccountId))?.AccountId?.Trim();
}

/// <summary>
/// The stored names of the Canadian tax forms the Canada recipe adds, which the T4 XML file and the data sets read.
/// </summary>
public static class CanadaTaxForms
{
    /// <summary>The T4 slips and T4 Summary.</summary>
    public const string FormT4 = "CA-T4";

    /// <summary>The RL-1 slips and RL-1 Summary.</summary>
    public const string FormRl1 = "CA-RL1";
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
/// <param name="DentalBenefitsCode">Box 45, the employer-offered dental benefits code (1 to 5).</param>
public sealed record T4XmlSlip(string Sin, string FirstName, string LastName, string AddressLine, string City, string Province, string Country, string PostalCode, string ProvinceOfEmployment, string EmployeeNumber, IReadOnlyDictionary<string, decimal> Boxes, int? DentalBenefitsCode = null);

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
    /// <summary>
    /// The boxes every slip writes, zero or not: the CRA reads a missing insurable or pensionable earnings amount as
    /// the employment income.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredAmounts = ["24", "26"];

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
                Optional(writer, "gvn_nm", Cut(slip.FirstName, 12));
                writer.WriteEndElement();
                writer.WriteStartElement("EMPE_ADDR");
                Optional(writer, "addr_l1_txt", Cut(slip.AddressLine, 30));
                Optional(writer, "cty_nm", Cut(slip.City, 28));
                Optional(writer, "prov_cd", slip.Province);
                writer.WriteElementString("cntry_cd", slip.Country ?? "CAN");
                Optional(writer, "pstl_cd", CanadaFilingSums.Normalize(slip.PostalCode));
                writer.WriteEndElement();
                writer.WriteElementString("sin", Digits(slip.Sin));

                if (!string.IsNullOrEmpty(slip.EmployeeNumber))
                {
                    writer.WriteElementString("empe_nbr", Cut(slip.EmployeeNumber, 20));
                }

                writer.WriteElementString("bn", CanadaFilingSums.Normalize(employer.BusinessNumber));
                writer.WriteElementString("rpt_tcd", "O");
                writer.WriteElementString("empt_prov_cd", slip.ProvinceOfEmployment);
                Optional(writer, "empr_dntl_ben_rpt_cd", slip.DentalBenefitsCode?.ToString(CultureInfo.InvariantCulture));
                writer.WriteStartElement("T4_AMT");

                foreach (var (box, element) in SlipAmounts)
                {
                    if (slip.Boxes.GetValueOrDefault(box) is var amount && (amount != 0 || RequiredAmounts.Contains(box)))
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
            Optional(writer, "addr_l1_txt", Cut(employer.AddressLine, 30));
            Optional(writer, "cty_nm", Cut(employer.City, 28));
            Optional(writer, "prov_cd", employer.Province);
            writer.WriteElementString("cntry_cd", employer.Country ?? "CAN");
            Optional(writer, "pstl_cd", CanadaFilingSums.Normalize(employer.PostalCode));
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
        Optional(writer, "cntc_nm", Cut(employer.ContactName, 22));

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

    // An optional element is left out rather than written empty.
    private static void Optional(XmlWriter writer, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteElementString(name, value);
        }
    }

    private static string Money(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Digits(string value)
        => new([.. (value ?? string.Empty).Where(char.IsAsciiDigit)]);

    private static string Cut(string value, int length)
        => value is null ? string.Empty : (value.Length > length ? value[..length] : value);
}
