namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// One choice on the "Choose how to pay" checkout step.
/// </summary>
public class InstallmentOptionViewModel
{
    /// <summary>
    /// Gets or sets the identifier posted when the choice is made.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the choice, for example "Pay in full" or "3 monthly payments".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets what the choice means in money: what is taken today and what follows.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets what is charged today.
    /// </summary>
    public decimal DueToday { get; set; }
}
