using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>
/// One row per pay profile: who, the employer, the schedule, the worker type and status. The tax id shows only its last
/// characters, and no bank number is a column. A reader of some employers reads theirs only.
/// </summary>
internal sealed class EmployeesDataSet : RecordDataSet<EmployeePayProfile>, IPayrollDataSet
{
    private readonly IPayProfileManager _profiles;
    private readonly IPayrollScopeService _scope;
    private readonly Services.PayrollNames _names;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmployeesDataSet"/> class.
    /// </summary>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="scope">The payroll scope.</param>
    /// <param name="names">The names.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmployeesDataSet(IPayProfileManager profiles, IPayrollScopeService scope, Services.PayrollNames names, IStringLocalizer<EmployeesDataSet> stringLocalizer)
        : base(new DataSetDescriptor(
            PayrollConstants.EmployeesDataSetName,
            stringLocalizer["Payroll employees"],
            stringLocalizer["One row per pay profile: the staff member, the employer, the pay schedule, the worker type, overtime and status. The tax id shows only its last characters."]))
    {
        _profiles = profiles;
        _scope = scope;
        _names = names;

        var S = stringLocalizer;
        var group = S["Pay profile"].Value;

        AddField("ItemId", S["ID"], DataFieldType.Text, profile => profile.ItemId, group).Identifier();
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, profile => profile.StaffMemberId, group);
        AddField("StaffMemberName", S["Staff member"], DataFieldType.Text, profile => _staffNames.GetValueOrDefault(profile.StaffMemberId ?? string.Empty), group);
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, profile => profile.EmployerId, group);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, profile => _employerNames.GetValueOrDefault(profile.EmployerId ?? string.Empty), group);
        AddField("PayScheduleId", S["Pay schedule ID"], DataFieldType.Text, profile => profile.PayScheduleId, group);
        AddField("PayScheduleName", S["Pay schedule"], DataFieldType.Text, profile => _scheduleNames.GetValueOrDefault(profile.PayScheduleId ?? string.Empty), group);
        AddField("WorkerType", S["Worker type"], DataFieldType.Text, profile => profile.WorkerType.ToString(), group);
        AddField("OvertimeStatus", S["Overtime"], DataFieldType.Text, profile => profile.OvertimeStatus.ToString(), group);
        AddField("Status", S["Status"], DataFieldType.Text, profile => profile.Status.ToString(), group);
        AddField("TaxIdLastFour", S["Tax id, last characters"], DataFieldType.Text, profile => profile.TaxId?.Hint, group);
        AddField("WorkSiteTeamId", S["Work site ID"], DataFieldType.Text, profile => profile.WorkSiteTeamId, group);
        AddField("CreatedUtc", S["Added"], DataFieldType.DateTime, profile => profile.CreatedUtc, group);
    }

    private Dictionary<string, string> _staffNames = new(StringComparer.Ordinal);
    private Dictionary<string, string> _employerNames = new(StringComparer.Ordinal);
    private Dictionary<string, string> _scheduleNames = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await _scope.GetScopeAsync(context?.User, PayrollPermissions.ViewPayrollReports, cancellationToken)).IsEmpty;

    /// <inheritdoc />
    protected override async IAsyncEnumerable<EmployeePayProfile> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scope = await _scope.GetScopeAsync(query?.Context?.User, PayrollPermissions.ViewPayrollReports, cancellationToken);

        if (scope.IsEmpty)
        {
            yield break;
        }

        var profiles = await _profiles.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds], cancellationToken);

        foreach (var profile in profiles)
        {
            _staffNames[profile.StaffMemberId] = await _names.StaffAsync(profile.StaffMemberId);

            if (profile.EmployerId is not null)
            {
                _employerNames[profile.EmployerId] = await _names.EmployerAsync(profile.EmployerId);
            }

            if (profile.PayScheduleId is not null)
            {
                _scheduleNames[profile.PayScheduleId] = await _names.ScheduleAsync(profile.PayScheduleId);
            }
        }

        foreach (var profile in profiles)
        {
            yield return profile;
        }
    }
}
