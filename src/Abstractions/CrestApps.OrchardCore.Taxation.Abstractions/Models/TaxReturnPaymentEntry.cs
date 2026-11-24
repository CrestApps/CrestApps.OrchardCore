namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A payment being recorded on a tax return. The payment form is built from it through
/// <c>IDisplayManager&lt;TaxReturnPaymentEntry&gt;</c>: Tax Returns' driver edits the amount, the day, the reference and
/// the method, and a pack adds a display driver that edits what its <c>ITaxReturnObserver</c> needs (such as the account
/// paid from) into the payment's <see cref="TaxReturnPayment.Properties"/>, checking <see cref="Return"/> so it shows only
/// for its returns.
/// </summary>
public sealed class TaxReturnPaymentEntry
{
    /// <summary>
    /// Gets or sets the return the payment is recorded on.
    /// </summary>
    public TaxReturn Return { get; set; }

    /// <summary>
    /// Gets or sets the payment.
    /// </summary>
    public TaxReturnPayment Payment { get; set; } = new();
}
