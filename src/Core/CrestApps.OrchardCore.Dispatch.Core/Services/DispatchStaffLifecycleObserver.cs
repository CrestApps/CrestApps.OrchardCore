using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Puts the visits a person who leaves was to make back on the board, unassigned, so a dispatcher gives them to
/// someone else. Visits already under way or done stay as they are.
/// </summary>
public sealed class DispatchStaffLifecycleObserver : IStaffLifecycleObserver
{
    private readonly IDispatchService _dispatchService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchStaffLifecycleObserver"/> class.
    /// </summary>
    /// <param name="dispatchService">The dispatch service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DispatchStaffLifecycleObserver(
        IDispatchService dispatchService,
        IClock clock,
        ILogger<DispatchStaffLifecycleObserver> logger)
    {
        _dispatchService = dispatchService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Terminated)
        {
            return;
        }

        var staff = context.StaffMember;
        var now = _clock.UtcNow;
        var visits = await _dispatchService.ListScheduledAsync(now, now.AddYears(1), [staff.ItemId], cancellationToken);

        foreach (var visit in visits.Where(visit => visit.Status is VisitStatus.Scheduled or VisitStatus.Dispatched))
        {
            var result = await _dispatchService.UnassignAsync(visit.ItemId, "Staff", cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Visit {VisitId} of staff member {StaffMemberId} who left could not be unassigned.", visit.ItemId, staff.ItemId);
            }
        }
    }
}
