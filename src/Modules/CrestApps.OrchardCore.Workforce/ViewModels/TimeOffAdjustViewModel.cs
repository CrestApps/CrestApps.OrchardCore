namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A change to a worker's balance by hand.
/// </summary>
public class TimeOffAdjustViewModel
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the kind: Opening, Adjustment, Carryover or Payout.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the day it counts from.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }
}
