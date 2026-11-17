using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// The names payroll pages show, read once per request: staff members, employers and pay schedules.
/// </summary>
public sealed class PayrollNames
{
    private readonly IStaffDirectory _staff;
    private readonly EmployerStore _employers;
    private readonly IPayScheduleManager _schedules;
    private Dictionary<string, string> _employerNames;
    private Dictionary<string, string> _scheduleNames;
    private readonly Dictionary<string, string> _staffNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollNames"/> class.
    /// </summary>
    /// <param name="staff">The staff directory.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="schedules">The pay schedules.</param>
    public PayrollNames(IStaffDirectory staff, EmployerStore employers, IPayScheduleManager schedules)
    {
        _staff = staff;
        _employers = employers;
        _schedules = schedules;
    }

    /// <summary>
    /// Gets a staff member's name.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <returns>The name, or the id when unknown.</returns>
    public async Task<string> StaffAsync(string staffMemberId)
    {
        if (string.IsNullOrEmpty(staffMemberId))
        {
            return null;
        }

        if (!_staffNames.TryGetValue(staffMemberId, out var name))
        {
            name = (await _staff.GetNamesAsync([staffMemberId])).GetValueOrDefault(staffMemberId) ?? staffMemberId;
            _staffNames[staffMemberId] = name;
        }

        return name;
    }

    /// <summary>
    /// Gets an employer's name.
    /// </summary>
    /// <param name="employerId">The employer.</param>
    /// <returns>The name, or the id when unknown.</returns>
    public async Task<string> EmployerAsync(string employerId)
    {
        _employerNames ??= (await _employers.ListAsync()).ToDictionary(employer => employer.ItemId, employer => employer.Name, StringComparer.Ordinal);

        return employerId is null ? null : _employerNames.GetValueOrDefault(employerId, employerId);
    }

    /// <summary>
    /// Gets a pay schedule's name.
    /// </summary>
    /// <param name="scheduleId">The pay schedule.</param>
    /// <returns>The name, or the id when unknown.</returns>
    public async Task<string> ScheduleAsync(string scheduleId)
    {
        _scheduleNames ??= (await _schedules.GetAllAsync()).ToDictionary(schedule => schedule.ItemId, schedule => schedule.Name, StringComparer.Ordinal);

        return scheduleId is null ? null : _scheduleNames.GetValueOrDefault(scheduleId, scheduleId);
    }
}

/// <summary>
/// What an editor changed in a pay profile's protected parts, for the observers once the profile is saved.
/// </summary>
public sealed class PayProfileEditTracker
{
    /// <summary>
    /// Gets what changed.
    /// </summary>
    public List<PayProfileChangeKind> Changes { get; } = [];
}
