using System.Globalization;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// A person's payroll records in a personal data export: their pay profiles (employer, schedule, status, the last
/// characters of the tax id, their elections and how net pay is paid, never an account number) and their final pay
/// statements. Payroll records are tax records the law makes the employer keep, so anonymizing changes none of them;
/// the export says so.
/// </summary>
internal sealed class PayrollPersonalDataContributor : IPersonalDataContributor
{
    public const string ProfileKind = "PayProfile";
    public const string StatementKind = "PayStatement";

    private readonly IStaffDirectory _staff;
    private readonly IPayProfileManager _profiles;
    private readonly PayStatementStore _statements;
    private readonly EmployerStore _employers;

    internal readonly IStringLocalizer S;

    public PayrollPersonalDataContributor(IStaffDirectory staff, IPayProfileManager profiles, PayStatementStore statements, EmployerStore employers, IStringLocalizer<PayrollPersonalDataContributor> stringLocalizer)
    {
        _staff = staff;
        _profiles = profiles;
        _statements = statements;
        _employers = employers;
        S = stringLocalizer;
    }

    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var staffMemberId in await StaffMemberIdsAsync(subject, cancellationToken))
        {
            foreach (var profile in await _profiles.ListByStaffMemberAsync(staffMemberId, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(profile.LegalName))
                {
                    subject.AddName(profile.LegalName);
                }
            }
        }
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var employers = (await _employers.GetAllAsync(cancellationToken)).ToDictionary(employer => employer.ItemId, employer => employer.Name, StringComparer.Ordinal);

        foreach (var staffMemberId in await StaffMemberIdsAsync(context.Subject, cancellationToken))
        {
            foreach (var profile in await _profiles.ListByStaffMemberAsync(staffMemberId, cancellationToken))
            {
                var record = context.Add(ProfileKind, profile.ItemId, employers.GetValueOrDefault(profile.EmployerId) ?? profile.EmployerId, profile.CreatedUtc);

                record.Fields["Employer"] = employers.GetValueOrDefault(profile.EmployerId);
                record.Fields[nameof(EmployeePayProfile.LegalName)] = profile.LegalName;
                record.Fields[nameof(EmployeePayProfile.Status)] = profile.Status.ToString();
                record.Fields["TaxIdLastCharacters"] = profile.TaxId?.Hint;
                record.Fields[nameof(EmployeePayProfile.HomeAddress)] = profile.HomeAddress is { } address
                    ? string.Join(", ", new[] { address.AddressLine1, address.AddressLine2, address.City, address.Region, address.PostalCode, address.Country }.Where(part => !string.IsNullOrWhiteSpace(part)))
                    : null;
                record.Fields["Elections"] = string.Join("; ", profile.Elections.Select(election => $"{election.Jurisdiction} from {election.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}: {string.Join(", ", election.Values.Select(pair => $"{pair.Key}={pair.Value}"))}"));
                record.Fields["NetPay"] = string.Join("; ", profile.Distribution.Select(row => row.Method == NetPayMethod.BankAccount ? S["Deposit"].Value : S["Check"].Value));
                record.Fields["Retention"] = S["Payroll records are kept as long as the tax law requires; they are not anonymized."].Value;
            }

            foreach (var statement in await _statements.ListAsync(index => index.StaffMemberId == staffMemberId && index.Status != nameof(PayStatementStatus.Draft), cancellationToken))
            {
                var record = context.Add(StatementKind, statement.ItemId, S["Pay statement {0}", statement.Number].Value, statement.CreatedUtc);

                record.Fields["Employer"] = statement.Snapshot?.EmployerName;
                record.Fields[nameof(PayStatement.Number)] = statement.Number;
                record.Fields[nameof(PayStatement.PayDate)] = statement.PayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["Period"] = $"{statement.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} - {statement.PeriodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
                record.Fields[nameof(PayStatement.Gross)] = statement.Gross.ToString(CultureInfo.InvariantCulture);
                record.Fields[nameof(PayStatement.Net)] = statement.Net.ToString(CultureInfo.InvariantCulture);
                record.Fields[nameof(PayStatement.Currency)] = statement.Currency;
                record.Fields[nameof(PayStatement.Status)] = statement.VoidOf is null ? statement.Status.ToString() : "Reversal";
            }
        }
    }

    public Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private async Task<IReadOnlyList<string>> StaffMemberIdsAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var ids = new List<string>();

        foreach (var userId in subject.UserIds)
        {
            if (await _staff.FindByUserIdAsync(userId, cancellationToken) is { } member)
            {
                ids.Add(member.ItemId);
            }
        }

        return ids;
    }
}
