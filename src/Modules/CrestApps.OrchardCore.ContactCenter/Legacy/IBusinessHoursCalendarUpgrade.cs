namespace CrestApps.OrchardCore.ContactCenter.Legacy;

/// <summary>
/// The upgrade of a site that kept Contact Center business-hours calendars: the calendars become Business Hours
/// schedules with the same ids, and the roles that edited calendars may edit the schedules.
/// </summary>
internal interface IBusinessHoursCalendarUpgrade
{
    /// <summary>
    /// Copies every stored calendar to a Business Hours schedule with the same id.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many calendars were copied.</returns>
    Task<int> MoveCalendarsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets the roles that edited calendars, and the roles that manage the Contact Center, edit Business Hours schedules,
    /// once the current request has committed.
    /// </summary>
    /// <returns>A task that completes when the grant is scheduled.</returns>
    Task GrantSchedulePermissionsAfterCommitAsync();
}
