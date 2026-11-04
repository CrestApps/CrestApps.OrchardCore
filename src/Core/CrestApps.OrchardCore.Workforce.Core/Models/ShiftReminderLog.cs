using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Models;

/// <summary>
/// Records that a shift reminder was sent, so it is sent once per shift and start, after a restart and on every node.
/// </summary>
public sealed class ShiftReminderLog : CatalogItem
{
    /// <summary>
    /// Gets or sets the reminder's key: the shift's event and the occurrence's start.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the worker reminded.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reminder was sent.
    /// </summary>
    public DateTime SentUtc { get; set; }
}
