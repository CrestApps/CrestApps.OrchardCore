using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The steps the Manage Calendars page takes around a new calendar: a calendar that belongs to a user starts as the creator's,
/// and the creator of a calendar they do not own keeps the right to manage it.
/// </summary>
internal static class CalendarCreation
{
    /// <summary>
    /// Makes a new calendar of a type, owned by the creator when the type's calendars belong to a user.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="type">The calendar type.</param>
    /// <param name="user">The person creating the calendar.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The new, unsaved calendar, or <see langword="null"/> when the type is unknown.</returns>
    public static async Task<Calendar> NewAsync(ICalendarService calendarService, string type, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarService);

        var calendar = await calendarService.NewAsync(type, cancellationToken);

        if (calendar?.OwnerKind == CalendarOwnerKind.User)
        {
            calendar.OwnerId = CalendarUser.GetId(user);
            calendar.Name = user?.Identity?.Name;
        }

        return calendar;
    }

    /// <summary>
    /// Gives the creator a Manage share on a calendar they do not own, so they can still change it after creating it.
    /// </summary>
    /// <param name="calendar">The new calendar.</param>
    /// <param name="user">The person creating the calendar.</param>
    public static void EnsureCreatorCanManage(Calendar calendar, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var userId = CalendarUser.GetId(user);

        if (string.IsNullOrEmpty(userId) || (calendar.OwnerKind == CalendarOwnerKind.User && calendar.OwnerId == userId))
        {
            return;
        }

        if (!calendar.Shares.Any(share => share.PrincipalKind == CalendarPrincipalKind.User && share.PrincipalId == userId && share.Access == CalendarAccessLevel.Manage))
        {
            calendar.Shares.Add(new CalendarShare
            {
                PrincipalKind = CalendarPrincipalKind.User,
                PrincipalId = userId,
                DisplayName = user?.Identity?.Name,
                Access = CalendarAccessLevel.Manage,
            });
        }
    }
}
