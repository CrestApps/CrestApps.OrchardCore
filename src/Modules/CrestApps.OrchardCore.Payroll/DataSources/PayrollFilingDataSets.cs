using System.Runtime.CompilerServices;
using System.Text;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Files;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>A liability period with its employer's name.</summary>
public sealed record TaxLiabilityRow(TaxLiabilityPeriod Period, string EmployerName);

/// <summary>
/// Tax liabilities: one row per employer, jurisdiction, group and deposit period, for those who may export payroll
/// data, filtered by the period's last day.
/// </summary>
public sealed class TaxLiabilitiesDataSet : RecordDataSet<TaxLiabilityRow>, IPayrollDataSet
{
    private readonly ITaxLiabilityService _liabilities;
    private readonly IEmployerManager _employers;
    private readonly PayrollReportReader _reader;
    private readonly ILocalClock _localClock;

    public TaxLiabilitiesDataSet(ITaxLiabilityService liabilities, IEmployerManager employers, PayrollReportReader reader, ILocalClock localClock, IStringLocalizer<TaxLiabilitiesDataSet> stringLocalizer)
        : base(new DataSetDescriptor("TaxLiabilities", stringLocalizer["Tax liabilities"], stringLocalizer["One row per employer, jurisdiction, group of taxes and deposit period: what the employees' and the employer's taxes owe, what was deposited, what is left and when it is due."]))
    {
        _liabilities = liabilities;
        _employers = employers;
        _reader = reader;
        _localClock = localClock;

        var S = stringLocalizer;
        var group = S["Liability"].Value;

        AddField("Key", S["Key"], DataFieldType.Text, row => row.Period.Key, group).Identifier();
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.Period.EmployerId, group);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, group);
        AddField("Jurisdiction", S["Jurisdiction"], DataFieldType.Text, row => row.Period.Jurisdiction, group);
        AddField("Group", S["Taxes"], DataFieldType.Text, row => row.Period.Group, group);
        AddField("Schedule", S["Deposit schedule"], DataFieldType.Text, row => row.Period.Schedule, group);
        AddField("PeriodStart", S["Period start"], DataFieldType.Date, row => row.Period.PeriodStart.ToDateTime(TimeOnly.MinValue), group);
        AddField("PeriodEnd", S["Period end"], DataFieldType.Date, row => row.Period.PeriodEnd.ToDateTime(TimeOnly.MinValue), group);
        AddField("DueDate", S["Due"], DataFieldType.Date, row => row.Period.DueDate.ToDateTime(TimeOnly.MinValue), group);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Period.Currency, group);
        AddField("EmployeeTaxes", S["Employees' taxes"], DataFieldType.Decimal, row => row.Period.EmployeeTaxes, group);
        AddField("EmployerTaxes", S["Employer's taxes"], DataFieldType.Decimal, row => row.Period.EmployerTaxes, group);
        AddField("Owed", S["Owed"], DataFieldType.Decimal, row => row.Period.Owed, group);
        AddField("Deposited", S["Deposited"], DataFieldType.Decimal, row => row.Period.Deposited, group);
        AddField("Balance", S["Balance"], DataFieldType.Decimal, row => row.Period.Balance, group);

        Descriptor.DefaultDateField = "PeriodEnd";
    }

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await _reader.GetScopeAsync(context?.User, PayrollPermissions.ExportPayrollData, cancellationToken)).IsEmpty;

    protected override async IAsyncEnumerable<TaxLiabilityRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scope = await _reader.GetScopeAsync(query?.Context?.User, PayrollPermissions.ExportPayrollData, cancellationToken);

        if (scope.IsEmpty)
        {
            yield break;
        }

        // Without dates, this year and the last, in the site's time zone.
        var (from, to) = DataConditions.DateRange(query.Conditions ?? [], "PeriodEnd");
        var year = (await _localClock.GetLocalNowAsync()).Year;
        var first = from.HasValue ? DateOnly.FromDateTime(from.Value) : new DateOnly(year - 1, 1, 1);
        var last = to.HasValue ? DateOnly.FromDateTime(to.Value) : new DateOnly(year, 12, 31);

        foreach (var employer in await _employers.GetAllAsync(cancellationToken))
        {
            if (!scope.All && !scope.EmployerIds.Contains(employer.ItemId))
            {
                continue;
            }

            foreach (var period in await _liabilities.ListAsync(employer.ItemId, first, last, cancellationToken))
            {
                yield return new TaxLiabilityRow(period, employer.Name);
            }
        }
    }
}

/// <summary>A person's wage statement of a filing, with its employer and the tax id when the reader may see it.</summary>
public sealed record WageRecordRow(PayrollFiling Filing, PayrollFilingPerson Person, Employer Employer, EmployeePayProfile Profile, string Ssn, string SubmitterUserId);

/// <summary>
/// W-2 wage records: one row per person of each employer's current wage statements (the draft or the filed ones, not
/// an amended one), with the employer's and the submitter's details the SSA file needs. The full social security
/// number is read only for a user who may see full tax ids of the employer, each one read is recorded, and it is
/// encrypted for the EFW2 file format: a preview or another file never shows it.
/// </summary>
public sealed class WageRecordsDataSet : RecordDataSet<WageRecordRow>, IPayrollDataSet
{
    /// <summary>The agency a registration names to carry the SSA submitter's user id as its account id.</summary>
    public const string SsaAgency = "SSA";

    /// <summary>The field of the social security number, encrypted: only the EFW2 file format reads it.</summary>
    public const string ProtectedSsnField = "ProtectedSsn";

    private readonly IPayrollFilingManager _filings;
    private readonly IEmployerManager _employers;
    private readonly IPayProfileManager _profiles;
    private readonly IPayProfileService _profileService;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollReportReader _reader;
    private readonly IDataProtectionProvider _dataProtection;

    public WageRecordsDataSet(IPayrollFilingManager filings, IEmployerManager employers, IPayProfileManager profiles, IPayProfileService profileService, IPayrollScopeService scope, PayrollReportReader reader, IDataProtectionProvider dataProtection, IStringLocalizer<WageRecordsDataSet> stringLocalizer)
        : base(new DataSetDescriptor("WageRecords", stringLocalizer["W-2 wage records"], stringLocalizer["One row per person of each employer's current Form W-2 filing: the employer and submitter, the person's name and address, their social security number encrypted for the EFW2 file format (only for those who may see full tax ids), and the boxes. Write them as an SSA EFW2 file with the EFW2 file format."]))
    {
        _filings = filings;
        _employers = employers;
        _profiles = profiles;
        _profileService = profileService;
        _scope = scope;
        _reader = reader;
        _dataProtection = dataProtection;

        var S = stringLocalizer;
        var employerGroup = S["Employer"].Value;
        var person = S["Employee"].Value;
        var boxes = S["Boxes"].Value;

        AddField("RecordId", S["Record ID"], DataFieldType.Text, row => $"{row.Filing.ItemId}:{row.Person.ProfileId}", person).Identifier();
        AddField("Year", S["Year"], DataFieldType.Integer, row => row.Filing.Year, employerGroup);
        AddField("EmployerEin", S["Employer EIN"], DataFieldType.Text, row => row.Employer?.TaxId, employerGroup);
        AddField("EmployerName", S["Employer name"], DataFieldType.Text, row => row.Employer?.LegalName ?? row.Employer?.Name, employerGroup);
        AddField("EmployerAddress", S["Employer address"], DataFieldType.Text, row => row.Employer?.Address?.AddressLine1, employerGroup);
        AddField("EmployerCity", S["Employer city"], DataFieldType.Text, row => row.Employer?.Address?.City, employerGroup);
        AddField("EmployerState", S["Employer state"], DataFieldType.Text, row => row.Employer?.Address?.Region, employerGroup);
        AddField("EmployerZip", S["Employer ZIP"], DataFieldType.Text, row => row.Employer?.Address?.PostalCode, employerGroup);
        AddField("SubmitterUserId", S["SSA user ID"], DataFieldType.Text, row => row.SubmitterUserId, employerGroup);
        AddField("ContactName", S["Contact name"], DataFieldType.Text, row => row.Employer?.ContactName, employerGroup);
        AddField("ContactPhone", S["Contact phone"], DataFieldType.Text, row => row.Employer?.ContactPhone, employerGroup);
        AddField("ContactEmail", S["Contact email"], DataFieldType.Text, row => row.Employer?.ContactEmail, employerGroup);
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.Person.StaffMemberId, person);
        AddField(ProtectedSsnField, S["Social security number (encrypted for the EFW2 file)"], DataFieldType.Text, row => row.Ssn, person);
        AddField("FirstName", S["First name"], DataFieldType.Text, row => Names(row.Person.Name).First, person);
        AddField("MiddleName", S["Middle name"], DataFieldType.Text, row => Names(row.Person.Name).Middle, person);
        AddField("LastName", S["Last name"], DataFieldType.Text, row => Names(row.Person.Name).Last, person);
        AddField("Address", S["Address"], DataFieldType.Text, row => row.Profile?.HomeAddress?.AddressLine1, person);
        AddField("City", S["City"], DataFieldType.Text, row => row.Profile?.HomeAddress?.City, person);
        AddField("State", S["State"], DataFieldType.Text, row => row.Profile?.HomeAddress?.Region, person);
        AddField("Zip", S["ZIP"], DataFieldType.Text, row => row.Profile?.HomeAddress?.PostalCode, person);

        foreach (var (box, _, _) in UnitedStatesEfw2.MoneyFields)
        {
            AddField("Box" + box, S["Box {0}", box], DataFieldType.Decimal, row => row.Person.Boxes.GetValueOrDefault(box), boxes);
        }

        AddField(OtherBoxesField, S["Other boxes"], DataFieldType.Text, row => UnitedStatesEfw2.Unwritten(row.Person.Boxes), boxes);
    }

    /// <summary>The field with the boxes the EFW2 file doesn't write, such as 12W 1200.00.</summary>
    public const string OtherBoxesField = "OtherBoxes";

    /// <summary>
    /// The parameter that reads a year's draft W-2 filing, such as an amendment being prepared, instead of the one filed.
    /// </summary>
    public const string DraftParameter = "Draft";

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await _reader.GetScopeAsync(context?.User, PayrollPermissions.ExportPayrollData, cancellationToken)).IsEmpty;

    protected override async IAsyncEnumerable<WageRecordRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var user = query?.Context?.User;
        var scope = await _reader.GetScopeAsync(user, PayrollPermissions.ExportPayrollData, cancellationToken);

        if (scope.IsEmpty)
        {
            yield break;
        }

        // One filing an employer and a year, so nobody is listed twice: the one filed, or the draft (a first one being
        // prepared, or an amendment when the Draft parameter asks for it); never both.
        var draft = query?.Parameters is { } parameters
            && parameters.TryGetValue(DraftParameter, out var value)
            && bool.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var wanted)
            && wanted;
        var filings = (await _filings.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds], cancellationToken))
            .Where(filing => filing.Form == UnitedStatesTaxForms.FormW2 && filing.Status != PayrollFilingStatus.Amended)
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
            var submitter = employer?.Registrations.FirstOrDefault(registration => string.Equals(registration.Agency, SsaAgency, StringComparison.OrdinalIgnoreCase))?.AccountId;
            var mayReveal = user is not null && await _scope.AuthorizeAsync(user, PayrollPermissions.ViewTaxIds, filing.EmployerId);

            foreach (var person in filing.People)
            {
                var profile = string.IsNullOrEmpty(person.ProfileId) ? null : await _profiles.FindByIdAsync(person.ProfileId, cancellationToken);
                var ssn = mayReveal && profile is not null ? await _profileService.RevealTaxIdAsync(profile, $"W-2 wage records {filing.Year}", cancellationToken) : null;

                yield return new WageRecordRow(filing, person, employer, profile, FileTaxIds.Protect(_dataProtection, ssn), submitter);
            }
        }
    }

    // A name as first, middle and last: the first word, the last word and what is between.
    private static (string First, string Middle, string Last) Names(string name)
    {
        var words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => (null, null, null),
            1 => (null, null, words[0]),
            _ => (words[0], words.Length > 2 ? string.Join(' ', words[1..^1]) : null, words[^1]),
        };
    }
}

/// <summary>
/// The SSA's EFW2 format, for the W-2 wage records data set: a Create a file step with it writes the file of the
/// year's wage statements. Rows are read by their field names; an employer is one EIN; a file holds one tax year, the
/// latest among the rows.
/// </summary>
public sealed class Efw2DataFileFormat : IDataFileFormat
{
    private readonly IDataProtectionProvider _dataProtection;

    /// <summary>
    /// Initializes a new instance of the <see cref="Efw2DataFileFormat"/> class.
    /// </summary>
    /// <param name="dataProtection">The data protection provider, to read the social security numbers.</param>
    public Efw2DataFileFormat(IDataProtectionProvider dataProtection)
    {
        _dataProtection = dataProtection;
    }

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<Efw2DataFileFormat>("SSA EFW2 (W-2 wage records)");

    /// <inheritdoc />
    public static string Name => "efw2";

    /// <inheritdoc />
    public static LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public static string Extension => ".txt";

    /// <inheritdoc />
    public static string ContentType => "text/plain";

    /// <inheritdoc />
    /// <returns>The number of people written.</returns>
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
            => row.TryGetValue(name, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null;

        decimal Money(Dictionary<string, object> row, string name)
            => row.TryGetValue(name, out var value) && value is not null ? Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture) : 0;

        // One tax year a file: the latest year among the rows.
        int YearOf(Dictionary<string, object> row)
            => int.TryParse(Text(row, "Year"), out var value) ? value : 0;

        var year = rows.Max(YearOf);

        rows = [.. rows.Where(row => YearOf(row) == year)];

        // Every person needs their full number: a row without it is the publisher not seeing tax ids, or a profile
        // with none. The file is refused rather than written with zeros.
        var ssns = rows.ToDictionary(row => row, row => FileTaxIds.Unprotect(_dataProtection, Text(row, WageRecordsDataSet.ProtectedSsnField)));
        var missing = rows.Where(row => new string([.. (ssns[row] ?? string.Empty).Where(char.IsAsciiDigit)]).Length != 9).ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"The EFW2 file needs each person's full social security number, and {missing.Count} rows have none (staff members {string.Join(", ", missing.Select(row => Text(row, "StaffMemberId")).Distinct(StringComparer.Ordinal))}): the user who published the pipeline needs View tax ids for the employer, and each person needs a tax id on file.");
        }

        // Amounts the file can't hold are refused rather than left out.
        var unwritten = rows.Where(row => !string.IsNullOrEmpty(Text(row, WageRecordsDataSet.OtherBoxesField))).ToList();

        if (unwritten.Count > 0)
        {
            throw new InvalidOperationException($"The EFW2 file doesn't write these boxes yet: {string.Join("; ", unwritten.Select(row => Text(row, "StaffMemberId") + ": " + Text(row, WageRecordsDataSet.OtherBoxesField)))}. File these wage statements another way.");
        }

        var first = rows[0];
        var employers = rows
            .GroupBy(row => Text(row, "EmployerEin") ?? string.Empty, StringComparer.Ordinal)
            .Select(group => new Efw2Employer(
                group.Key,
                Text(group.First(), "EmployerName"),
                new Efw2Address(null, Text(group.First(), "EmployerAddress"), Text(group.First(), "EmployerCity"), Text(group.First(), "EmployerState"), Text(group.First(), "EmployerZip"), null),
                [.. group.Select(row => new Efw2Person(
                    ssns[row],
                    Text(row, "FirstName"),
                    Text(row, "MiddleName"),
                    Text(row, "LastName"),
                    new Efw2Address(null, Text(row, "Address"), Text(row, "City"), Text(row, "State"), Text(row, "Zip"), null),
                    UnitedStatesEfw2.MoneyFields.ToDictionary(field => field.Box, field => Money(row, "Box" + field.Box), StringComparer.Ordinal)))]))
            .ToList();
        var submitter = new Efw2Submitter(
            Text(first, "EmployerEin"),
            Text(first, "SubmitterUserId"),
            Text(first, "EmployerName"),
            employers[0].Address,
            Text(first, "ContactName"),
            Text(first, "ContactPhone"),
            Text(first, "ContactEmail"));
        var bytes = Encoding.ASCII.GetBytes(UnitedStatesEfw2.Write(submitter, employers, year));

        await stream.WriteAsync(bytes, cancellationToken);

        return rows.Count;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<DataBatch> ReadAsync(Stream stream, DataFileOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The EFW2 format is written, never read.");
}
