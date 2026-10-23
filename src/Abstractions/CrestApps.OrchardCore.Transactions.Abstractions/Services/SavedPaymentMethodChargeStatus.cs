namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// How a charge of a saved payment method made by <see cref="ITransactionCommandService.ChargeSavedPaymentMethodAsync"/>
/// ended.
/// </summary>
public enum SavedPaymentMethodChargeStatus
{
    /// <summary>
    /// Nothing was charged: the command was refused before the payment provider was asked, for example because the
    /// transaction owes nothing or the payment method is not the owner's.
    /// </summary>
    NotCharged = 0,

    /// <summary>
    /// The payment was taken and recorded on the transaction.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// The provider accepted the charge but has not confirmed it yet. The reconciliation sweep records it on the
    /// transaction once it settles, so it must not be charged again meanwhile.
    /// </summary>
    Pending = 2,

    /// <summary>
    /// The provider refused the payment, for example a declined or expired card.
    /// </summary>
    Declined = 3,

    /// <summary>
    /// The card's bank asked the cardholder to approve the payment, which can't be done without them. The owner has to
    /// pay it themselves, for example from the invoice's pay link.
    /// </summary>
    RequiresAuthentication = 4,

    /// <summary>
    /// The payment could not be started at the provider, for example because it could not be reached. Nothing was
    /// charged, and the charge can be tried again.
    /// </summary>
    Failed = 5,
}
