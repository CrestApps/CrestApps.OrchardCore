namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// A discount on an agent checkout.
/// </summary>
public sealed class AgentAppliedDiscount
{
    /// <summary>
    /// Gets or sets the code that gave it, or <see langword="null"/> for an automatic one.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets its description.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the amount, positive.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it applies without a code.
    /// </summary>
    public bool Automatic { get; set; }
}
