namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for a lead status.
/// </summary>
public class LeadStatusViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the lead status is new.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the Order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets whether a new lead starts in this status. Only an open status can be the default.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets whether the status is open, closed or the converted status.
    /// </summary>
    public LeadStatusKind Kind { get; set; }
}

/// <summary>
/// The kinds of lead status. A status is exactly one of them, which is why the editor offers one choice rather than
/// separate closed and converted flags that could contradict each other.
/// </summary>
public enum LeadStatusKind
{
    /// <summary>
    /// Leads in the status are still worked and loaded.
    /// </summary>
    Open,

    /// <summary>
    /// Leads in the status are finished without being converted, and inventory loads skip them by default.
    /// </summary>
    Closed,

    /// <summary>
    /// The status a lead takes when it is converted. It is closed, and it cannot be chosen by hand.
    /// </summary>
    Converted,
}
