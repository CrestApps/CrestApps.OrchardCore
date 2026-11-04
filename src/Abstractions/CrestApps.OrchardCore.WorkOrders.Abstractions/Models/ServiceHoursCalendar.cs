namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// A calendar of opening hours a service level policy can run its clock in, as an
/// <see cref="Services.IServiceHoursProvider"/> lists it.
/// </summary>
/// <param name="Id">The calendar id the policy stores in <see cref="ServiceLevelPolicy.HoursCalendarId"/>.</param>
/// <param name="Name">The calendar's name, for the policy editor.</param>
public sealed record ServiceHoursCalendar(string Id, string Name);
