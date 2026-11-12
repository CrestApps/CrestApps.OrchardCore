using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Takes the published shifts of a person who leaves off the schedule, from the day after their last working day (or
/// from now when they leave at once), so planners see the gaps and open shifts can be posted.
/// </summary>
internal sealed class WorkforceStaffLifecycleObserver : IStaffLifecycleObserver
{
    private readonly IShiftService _shiftService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public WorkforceStaffLifecycleObserver(
        IShiftService shiftService,
        IClock clock,
        ILogger<WorkforceStaffLifecycleObserver> logger)
    {
        _shiftService = shiftService;
        _clock = clock;
        _logger = logger;
    }

    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Terminated)
        {
            return;
        }

        var staff = context.StaffMember;
        var now = _clock.UtcNow;
        var fromUtc = staff.TerminationDate is { } last && last.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) > now
            ? last.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            : now;

        foreach (var shift in await _shiftService.ListWorkerShiftsAsync(staff.ItemId, fromUtc, fromUtc.AddYears(1), cancellationToken))
        {
            var result = await _shiftService.RemoveAsync(shift.ItemId, ScheduleChangeInitiator.Employer, "Left the business", "Staff", cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("A shift of staff member {StaffMemberId} who left could not be removed: {Errors}", staff.ItemId, string.Join(" ", result.Errors));
            }
        }
    }
}
