using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Files;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>
/// A T4 slip of a filing, with what the T4 XML file needs.
/// </summary>
/// <param name="Filing">The filing.</param>
/// <param name="Person">The person's slip.</param>
/// <param name="Employer">The employer.</param>
/// <param name="Profile">The person's pay profile.</param>
/// <param name="Sin">The full social insurance number, or <see langword="null"/>.</param>
public sealed record T4SlipRow(PayrollFiling Filing, PayrollFilingPerson Person, Employer Employer, EmployeePayProfile Profile, string Sin);

/// <summary>
/// One row per person of each employer's current T4 filing: the employer, the person's name, address and social
/// insurance number (only for those who may see full tax ids), the province of employment and the boxes, with the T4
/// Summary's employer amounts. Write them as the CRA's T4 XML file with the T4 XML file format.
/// </summary>
public sealed class T4SlipsDataSet : RecordDataSet<T4SlipRow>, IPayrollDataSet
{
    /// <summary>The parameter that reads the draft (a first filing being prepared, or an amendment).</summary>
    public const string DraftParameter = "Draft";

    private readonly IPayrollFilingManager _filings;
    private readonly IEmployerManager _employers;
    private readonly IPayProfileManager _profiles;
    private readonly IPayProfileService _profileService;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollReportReader _reader;

    /// <summary>
    /// Initializes a new instance of the <see cref="T4SlipsDataSet"/> class.
    /// </summary>
    /// <param name="filings">The filings.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="profileService">The pay profile service, to reveal tax ids.</param>
    /// <param name="scope">The employer scope.</param>
    /// <param name="reader">The report reader.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public T4SlipsDataSet(IPayrollFilingManager filings, IEmployerManager employers, IPayProfileManager profiles, IPayProfileService profileService, IPayrollScopeService scope, PayrollReportReader reader, IStringLocalizer<T4SlipsDataSet> stringLocalizer)
        : base(new DataSetDescriptor("T4Slips", stringLocalizer["T4 slips"], stringLocalizer["One row per person of each employer's current T4 filing: the employer's business number, the person's name, address and social insurance number (only for those who may see full tax ids), the province of employment and the boxes. Write them as the CRA's T4 XML file with the T4 XML file format."]))
    {
        _filings = filings;
        _employers = employers;
        _profiles = profiles;
        _profileService = profileService;
        _scope = scope;
        _reader = reader;

        var S = stringLocalizer;
        var employerGroup = S["Employer"].Value;
        var person = S["Employee"].Value;
        var boxes = S["Boxes"].Value;

        AddField("RecordId", S["Record ID"], DataFieldType.Text, row => $"{row.Filing.ItemId}:{row.Person.ProfileId}", person).Identifier();
        AddField("Year", S["Year"], DataFieldType.Integer, row => row.Filing.Year, employerGroup);
        AddField("BusinessNumber", S["Business number (RP)"], DataFieldType.Text, row => CanadaFilingSums.PayrollAccount(row.Employer), employerGroup);
        AddField("EmployerName", S["Employer name"], DataFieldType.Text, row => row.Employer?.LegalName ?? row.Employer?.Name, employerGroup);
        AddField("EmployerAddress", S["Employer address"], DataFieldType.Text, row => row.Employer?.Address?.AddressLine1, employerGroup);
        AddField("EmployerCity", S["Employer city"], DataFieldType.Text, row => row.Employer?.Address?.City, employerGroup);
        AddField("EmployerProvince", S["Employer province"], DataFieldType.Text, row => row.Employer?.Address?.Region?.ToUpperInvariant(), employerGroup);
        AddField("EmployerPostalCode", S["Employer postal code"], DataFieldType.Text, row => row.Employer?.Address?.PostalCode, employerGroup);
        AddField("ContactName", S["Contact name"], DataFieldType.Text, row => row.Employer?.ContactName, employerGroup);
        AddField("ContactPhone", S["Contact phone"], DataFieldType.Text, row => row.Employer?.ContactPhone, employerGroup);
        AddField("ContactEmail", S["Contact email"], DataFieldType.Text, row => row.Employer?.ContactEmail, employerGroup);
        AddField("EmployerCpp", S["Employer's CPP (27)"], DataFieldType.Decimal, row => row.Filing.Boxes.GetValueOrDefault("27"), employerGroup);
        AddField("EmployerCpp2", S["Employer's second CPP (27A)"], DataFieldType.Decimal, row => row.Filing.Boxes.GetValueOrDefault("27A"), employerGroup);
        AddField("EmployerEi", S["Employer's EI (19)"], DataFieldType.Decimal, row => row.Filing.Boxes.GetValueOrDefault("19"), employerGroup);
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.Person.StaffMemberId, person);
        AddField("Sin", S["Social insurance number"], DataFieldType.Text, row => row.Sin, person);
        AddField("FirstName", S["Given name"], DataFieldType.Text, row => Names(row.Person.Name).First, person);
        AddField("LastName", S["Surname"], DataFieldType.Text, row => Names(row.Person.Name).Last, person);
        AddField("Address", S["Address"], DataFieldType.Text, row => row.Profile?.HomeAddress?.AddressLine1, person);
        AddField("City", S["City"], DataFieldType.Text, row => row.Profile?.HomeAddress?.City, person);
        AddField("Province", S["Province"], DataFieldType.Text, row => row.Profile?.HomeAddress?.Region?.ToUpperInvariant(), person);
        AddField("PostalCode", S["Postal code"], DataFieldType.Text, row => row.Profile?.HomeAddress?.PostalCode, person);
        AddField("ProvinceOfEmployment", S["Province of employment (10)"], DataFieldType.Text, row => ProvinceOfEmployment(row), person);

        foreach (var (box, _) in CanadaT4Xml.SlipAmounts)
        {
            AddField("Box" + box, S["Box {0}", box], DataFieldType.Decimal, row => row.Person.Boxes.GetValueOrDefault(box), boxes);
        }
    }

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await _reader.GetScopeAsync(context?.User, PayrollPermissions.ExportPayrollData, cancellationToken)).IsEmpty;

    /// <inheritdoc />
    protected override async IAsyncEnumerable<T4SlipRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var user = query?.Context?.User;
        var scope = await _reader.GetScopeAsync(user, PayrollPermissions.ExportPayrollData, cancellationToken);

        if (scope.IsEmpty)
        {
            yield break;
        }

        // One filing an employer and a year: the one filed, or the draft when the Draft parameter asks for it.
        var draft = query?.Parameters is { } parameters
            && parameters.TryGetValue(DraftParameter, out var value)
            && bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var wanted)
            && wanted;
        var filings = (await _filings.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds], cancellationToken))
            .Where(filing => filing.Form == FormT4.FormName && filing.Status != PayrollFilingStatus.Amended)
            .GroupBy(filing => (filing.EmployerId, filing.Year))
            .Select(group =>
            {
                var filed = group.Where(filing => filing.Status == PayrollFilingStatus.Filed).OrderBy(filing => filing.FiledOn).ThenBy(filing => filing.CreatedUtc).LastOrDefault();
                var prepared = group.Where(filing => filing.Status == PayrollFilingStatus.Draft).OrderBy(filing => filing.CreatedUtc).LastOrDefault();

                return draft ? prepared ?? filed : filed ?? prepared;
            })
            .Where(filing => filing is not null);

        foreach (var filing in filings.OrderBy(filing => filing.Year))
        {
            var employer = await _employers.FindByIdAsync(filing.EmployerId, cancellationToken);
            var mayReveal = user is not null && await _scope.AuthorizeAsync(user, PayrollPermissions.ViewTaxIds, filing.EmployerId);

            foreach (var person in filing.People)
            {
                var profile = string.IsNullOrEmpty(person.ProfileId) ? null : await _profiles.FindByIdAsync(person.ProfileId, cancellationToken);
                var sin = mayReveal && profile is not null ? await _profileService.RevealTaxIdAsync(profile, $"T4 slips {filing.Year}", cancellationToken) : null;

                yield return new T4SlipRow(filing, person, employer, profile, sin);
            }
        }
    }

    // Box 10: the province of employment: Quebec when QPP was paid, else the province of the person's home.
    private static string ProvinceOfEmployment(T4SlipRow row)
        => row.Person.Boxes.TryGetValue("17", out var qpp) && qpp != 0
            ? "QC"
            : row.Profile?.HomeAddress?.Region?.ToUpperInvariant();

    private static (string First, string Last) Names(string name)
    {
        var words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => (null, null),
            1 => (null, words[0]),
            _ => (string.Join(' ', words[..^1]), words[^1]),
        };
    }
}

/// <summary>
/// Writes T4 slips as the CRA's T4 XML file (Internet file transfer), one employer and one tax year a file: the latest
/// year and the first business number among the rows. A slip with no full social insurance number, or an employer with
/// no business number and payroll program account, is refused rather than written.
/// </summary>
public sealed class T4XmlDataFileFormat : IDataFileFormat
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<T4XmlDataFileFormat>("CRA T4 XML (T4 slips and Summary)");

    /// <summary>Gets the stored name.</summary>
    public static string Name => "t4xml";

    /// <summary>Gets what people see.</summary>
    public static LocalizationSource DisplayName => _displayName;

    /// <summary>Gets the file's extension.</summary>
    public static string Extension => ".xml";

    /// <summary>Gets the file's media type.</summary>
    public static string ContentType => "application/xml";

    /// <inheritdoc />
    public async Task<long> WriteAsync(Stream stream, IReadOnlyList<DataField> fields, IAsyncEnumerable<DataBatch> batches, DataFileOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(batches);

        var rows = new List<Dictionary<string, object>>();

        await foreach (var batch in batches.WithCancellation(cancellationToken))
        {
            foreach (var row in batch.Rows)
            {
                var values = new Dictionary<string, object>(StringComparer.Ordinal);

                for (var i = 0; i < batch.Fields.Count && i < row.Length; i++)
                {
                    values[batch.Fields[i].Name] = row[i];
                }

                rows.Add(values);
            }
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        string Text(Dictionary<string, object> row, string name)
            => row.TryGetValue(name, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

        decimal Money(Dictionary<string, object> row, string name)
            => row.TryGetValue(name, out var value) && value is not null ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : 0;

        int YearOf(Dictionary<string, object> row)
            => int.TryParse(Text(row, "Year"), out var value) ? value : 0;

        var year = rows.Max(YearOf);
        var businessNumber = rows.Where(row => YearOf(row) == year).Select(row => Text(row, "BusinessNumber")).FirstOrDefault(number => !string.IsNullOrEmpty(number));

        rows = [.. rows.Where(row => YearOf(row) == year && Text(row, "BusinessNumber") == businessNumber)];

        if (!CanadaFilingSums.IsPayrollAccount(businessNumber))
        {
            throw new InvalidOperationException("The T4 XML file needs the employer's CRA business number with its payroll program account (such as 123456789RP0001) on its CA registration.");
        }

        var missing = rows.Where(row => new string([.. (Text(row, "Sin") ?? string.Empty).Where(char.IsAsciiDigit)]).Length != 9).ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"The T4 XML file needs each person's full social insurance number, and {missing.Count} rows have none (staff members {string.Join(", ", missing.Select(row => Text(row, "StaffMemberId")).Distinct(StringComparer.Ordinal))}): the user who published the pipeline needs View tax ids for the employer, and each person needs a tax id on file.");
        }

        var first = rows[0];
        var employer = new T4XmlEmployer(
            businessNumber,
            Text(first, "EmployerName"),
            Text(first, "EmployerAddress"),
            Text(first, "EmployerCity"),
            Text(first, "EmployerProvince"),
            "CAN",
            Text(first, "EmployerPostalCode"),
            Text(first, "ContactName"),
            Text(first, "ContactPhone"),
            Text(first, "ContactEmail"));
        var slips = rows.Select(row => new T4XmlSlip(
            Text(row, "Sin"),
            Text(row, "FirstName"),
            Text(row, "LastName"),
            Text(row, "Address"),
            Text(row, "City"),
            Text(row, "Province"),
            "CAN",
            Text(row, "PostalCode"),
            Text(row, "ProvinceOfEmployment"),
            null,
            CanadaT4Xml.SlipAmounts.ToDictionary(field => field.Box, field => Money(row, "Box" + field.Box), StringComparer.Ordinal))).ToList();
        var summary = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["19"] = Money(first, "EmployerEi"),
            ["27"] = Money(first, "EmployerCpp"),
            ["27A"] = Money(first, "EmployerCpp2"),
        };

        await stream.WriteAsync(Encoding.UTF8.GetBytes(CanadaT4Xml.Write(employer, slips, year, summary)), cancellationToken);

        return rows.Count;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<DataBatch> ReadAsync(Stream stream, DataFileOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The T4 XML format is written, never read.");
}
