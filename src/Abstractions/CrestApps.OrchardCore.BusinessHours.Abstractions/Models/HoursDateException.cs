namespace CrestApps.OrchardCore.BusinessHours.Models;

/// <summary>
/// A date that doesn't follow the weekly windows: a holiday closed all day, or a day with special hours. The date owns
/// its whole local day, from midnight to midnight.
/// </summary>
public sealed class HoursDateException
{
    /// <summary>
    /// Gets or sets the local date.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets what the date is, such as "New Year's Day"; optional.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the date is closed all day. When <see langword="false"/>, the date is open only from
    /// <see cref="OpenMinute"/> to <see cref="CloseMinute"/>.
    /// </summary>
    public bool Closed { get; set; } = true;

    /// <summary>
    /// Gets or sets the local time, in minutes from midnight, the special hours open.
    /// </summary>
    public int OpenMinute { get; set; }

    /// <summary>
    /// Gets or sets the local time, in minutes from midnight (exclusive), the special hours close. The same minute as
    /// <see cref="OpenMinute"/> means open all day; an earlier minute runs past midnight into the next day.
    /// </summary>
    public int CloseMinute { get; set; }
}
