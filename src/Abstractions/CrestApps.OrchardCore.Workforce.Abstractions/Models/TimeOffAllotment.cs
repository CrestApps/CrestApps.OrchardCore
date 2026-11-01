using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How many people of a team (and its sub-teams) may be off on a day, and whether requests within that are approved
/// with no manager.
/// </summary>
public sealed class TimeOffAllotment : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the allotment name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team the allotment covers, with its sub-teams.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the time-off types it counts; empty for every type that counts against allotments.
    /// </summary>
    public IList<string> TypeIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many people may be off on each weekday, Sunday first: seven numbers, or none for nobody.
    /// </summary>
    public IList<int> WeekdaySlots { get; set; } = [];

    /// <summary>
    /// Gets or sets dates with their own number, such as a holiday.
    /// </summary>
    public IList<TimeOffAllotmentDate> Dates { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a request that fits is approved with no manager.
    /// </summary>
    public bool AutoApprove { get; set; } = true;

    /// <summary>
    /// Gets or sets what happens to a request that doesn't fit.
    /// </summary>
    public TimeOffAllotmentFullAction WhenFull { get; set; } = TimeOffAllotmentFullAction.Waitlist;

    /// <summary>
    /// Gets or sets whether the allotment is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the allotment was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the allotment was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns how many people may be off on a date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The number of places.</returns>
    public int SlotsOn(DateOnly date)
    {
        var special = Dates.FirstOrDefault(item => item.Date == date);

        if (special is not null)
        {
            return Math.Max(0, special.Slots);
        }

        var weekday = (int)date.DayOfWeek;

        return weekday < WeekdaySlots.Count ? Math.Max(0, WeekdaySlots[weekday]) : 0;
    }

    /// <summary>
    /// Returns whether the allotment counts a time-off type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool Counts(TimeOffType type)
    {
        return type is not null && type.CountsAgainstAllotment && (TypeIds.Count == 0 || TypeIds.Contains(type.ItemId));
    }
}
