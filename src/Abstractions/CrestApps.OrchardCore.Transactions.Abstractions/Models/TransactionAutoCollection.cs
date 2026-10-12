namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// Describes how a <see cref="Transaction"/> will be collected on its due date without the owner acting, so the
/// owner can be told before it happens and an administrator can see it. It describes the arrangement only; the
/// component that charges the payment method holds the references needed to do so.
/// </summary>
public sealed class TransactionAutoCollection
{
    /// <summary>
    /// Gets or sets the key of the payment provider that will take the payment.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets a description of the payment method for a person, for example <c>Visa •••• 4242</c>.
    /// </summary>
    public string PaymentMethodDescription { get; set; }
}
