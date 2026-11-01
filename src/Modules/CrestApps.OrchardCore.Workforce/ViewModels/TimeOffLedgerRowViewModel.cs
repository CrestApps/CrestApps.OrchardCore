using CrestApps.OrchardCore.Workforce.Models;
namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A row of a worker's time-off ledger.
/// </summary>
public class TimeOffLedgerRowViewModel
{
    /// <summary>
    /// Gets or sets the entry.
    /// </summary>
    public TimeOffLedgerEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets the policy's name.
    /// </summary>
    public string PolicyName { get; set; }

    /// <summary>
    /// Gets or sets what the policy's type is counted in.
    /// </summary>
    public TimeOffUnit Unit { get; set; }
}
