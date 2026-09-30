namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for a opportunity stage.
/// </summary>
public class OpportunityStageViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the opportunity stage is new.
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
    /// Gets or sets the Probability.
    /// </summary>
    public int Probability { get; set; }

    /// <summary>
    /// Gets or sets the IsClosed.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets the IsWon.
    /// </summary>
    public bool IsWon { get; set; }
}
