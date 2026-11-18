using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundJobs;

namespace CrestApps.OrchardCore.ContactCenter.Legacy;

/// <summary>
/// Copies the calendars through <see cref="BusinessHoursCalendarMove"/> and grants the schedule permission after the
/// request commits.
/// </summary>
internal sealed class BusinessHoursCalendarUpgrade : IBusinessHoursCalendarUpgrade
{
    /// <summary>
    /// The permission that let a role edit the calendars, which no longer exists.
    /// </summary>
    internal const string LegacyManagePermissionName = "ManageContactCenterBusinessHours";

    private readonly BusinessHoursCalendarMove _move;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarUpgrade"/> class.
    /// </summary>
    /// <param name="move">The calendar move.</param>
    public BusinessHoursCalendarUpgrade(BusinessHoursCalendarMove move)
    {
        _move = move;
    }

    /// <inheritdoc/>
    public Task<int> MoveCalendarsAsync(CancellationToken cancellationToken = default)
        => _move.MoveAsync(cancellationToken);

    /// <inheritdoc/>
    public Task GrantSchedulePermissionsAfterCommitAsync()
    {
        // The roles are documents; they are written after the request that runs the upgrade, once its transaction has
        // committed. Granting is idempotent, so a later run changes nothing more.
        return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("grant-business-hours-schedules", static scope => RolePermissionMigrations.GrantAsync(
            scope.ServiceProvider,
            new Dictionary<string, string[]>
            {
                [LegacyManagePermissionName] = [BusinessHoursPermissions.ManageBusinessHoursSchedules.Name],
                [ContactCenterPermissions.ManageContactCenter.Name] = [BusinessHoursPermissions.ManageBusinessHoursSchedules.Name],
            },
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<BusinessHoursCalendarUpgrade>()));
    }
}
