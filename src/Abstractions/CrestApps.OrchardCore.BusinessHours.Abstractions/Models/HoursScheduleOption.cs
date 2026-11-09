namespace CrestApps.OrchardCore.BusinessHours.Models;

/// <summary>
/// A schedule an editor can pick, such as a support desk's hours calendar.
/// </summary>
/// <param name="Id">The schedule identifier.</param>
/// <param name="Name">The schedule name.</param>
public sealed record HoursScheduleOption(string Id, string Name);
