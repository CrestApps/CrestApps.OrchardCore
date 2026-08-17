namespace CrestApps.OrchardCore.PayLater.Models;

/// <summary>
/// The site settings that control how Pay Later commitments become outstanding transactions.
/// </summary>
public sealed class PayLaterSettings
{
    /// <summary>
    /// Gets or sets the number of days a Pay Later balance is allowed before it is due. When greater than
    /// zero the outstanding transaction is created with a due date this many days after checkout, which
    /// drives the reminder cadence. A value of 0 leaves the balance without a due date. Defaults to 30.
    /// </summary>
    public int NetTermDays { get; set; } = 30;
}
