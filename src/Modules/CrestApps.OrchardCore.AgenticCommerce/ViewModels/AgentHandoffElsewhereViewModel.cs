namespace CrestApps.OrchardCore.AgenticCommerce.ViewModels;

/// <summary>
/// The page a hand-off link shows in a browser that isn't the one the checkout was given to. It says only whether the
/// order was placed: whoever holds the link sees nothing of the order itself.
/// </summary>
public class AgentHandoffElsewhereViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the order was placed; otherwise the checkout is still open in the other
    /// browser.
    /// </summary>
    public bool Placed { get; set; }
}
