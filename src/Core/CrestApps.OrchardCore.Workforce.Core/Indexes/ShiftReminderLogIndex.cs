using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of sent shift reminders.
/// </summary>
public sealed class ShiftReminderLogIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the reminder's key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reminder was sent.
    /// </summary>
    public DateTime SentUtc { get; set; }
}
