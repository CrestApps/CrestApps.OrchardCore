namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The fields of a time-off type.
/// </summary>
public class TimeOffTypeEditViewModel
{
    /// <summary>
    /// Gets or sets the type id, or empty for a new type.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the unit.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets the hours a day counts with no shift.
    /// </summary>
    public decimal HoursPerDay { get; set; }

    /// <summary>
    /// Gets or sets whether it is paid.
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// Gets or sets whether it needs a balance.
    /// </summary>
    public bool RequiresBalance { get; set; }

    /// <summary>
    /// Gets or sets whether part of a day can be asked for.
    /// </summary>
    public bool AllowPartialDay { get; set; }

    /// <summary>
    /// Gets or sets whether a reason is needed.
    /// </summary>
    public bool RequiresReason { get; set; }

    /// <summary>
    /// Gets or sets whether it takes a place in allotments.
    /// </summary>
    public bool CountsAgainstAllotment { get; set; }

    /// <summary>
    /// Gets or sets the pay code.
    /// </summary>
    public string PayCode { get; set; }

    /// <summary>
    /// Gets or sets whether workers can request it themselves.
    /// </summary>
    public bool VisibleToWorkers { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }
}
