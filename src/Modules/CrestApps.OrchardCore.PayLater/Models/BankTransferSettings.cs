namespace CrestApps.OrchardCore.PayLater.Models;

/// <summary>
/// The site settings of the Bank transfer payment method: whether it is offered, how to pay, and when it is due.
/// </summary>
public sealed class BankTransferSettings
{
    /// <summary>
    /// Gets or sets whether Bank transfer is offered at checkout. Off until the store adds its bank details.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets how to pay, shown at checkout, on the confirmation and on the order's page: the store's bank
    /// account details, for example.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets how many days after checkout the transfer is due. 0 records the balance without a due date.
    /// </summary>
    public int NetTermDays { get; set; } = 7;
}
