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
    /// Gets or sets the IsDefault.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the IsClosed.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets the IsConverted.
    /// </summary>
    public bool IsConverted { get; set; }
}
