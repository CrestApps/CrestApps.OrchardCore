using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A kind of time off, such as vacation, sick time or jury duty.
/// </summary>
public sealed class TimeOffType : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the type name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the type is for, shown to workers.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the color time off of the type is drawn in.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets what the type is counted in.
    /// </summary>
    public TimeOffUnit Unit { get; set; } = TimeOffUnit.Hours;

    /// <summary>
    /// Gets or sets how many hours a day of time off counts when the worker has no shift that day.
    /// </summary>
    public decimal HoursPerDay { get; set; } = 8;

    /// <summary>
    /// Gets or sets whether the time off is paid.
    /// </summary>
    public bool IsPaid { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a request needs enough balance under a policy of the type.
    /// </summary>
    public bool RequiresBalance { get; set; } = true;

    /// <summary>
    /// Gets or sets whether part of a day can be requested.
    /// </summary>
    public bool AllowPartialDay { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a request needs a reason.
    /// </summary>
    public bool RequiresReason { get; set; }

    /// <summary>
    /// Gets or sets whether time off of the type takes a place in an allotment.
    /// </summary>
    public bool CountsAgainstAllotment { get; set; } = true;

    /// <summary>
    /// Gets or sets the pay code the time off is paid under.
    /// </summary>
    public string PayCode { get; set; }

    /// <summary>
    /// Gets or sets whether workers can request the type themselves.
    /// </summary>
    public bool VisibleToWorkers { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the type can be used.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the type was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the type was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
